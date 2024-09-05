namespace Datus.LockStep
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Dapper;
    using Npgsql;

    class ScriptRunner: IDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly string _pathSpecification;
        private readonly Direction _direction;

        public ScriptRunner(string connectionString, string pathSpecification, Direction direction)
        {
            _connection = new NpgsqlConnection(connectionString);
            _pathSpecification = pathSpecification;
            _direction =  direction;
        }

        private void CreateMigrationTableIfNeeded()
        {
            var exists = _connection.ExecuteScalar<bool>("SELECT COUNT(*) = 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__lockstep'");
            if (!exists) {
                var script = @"
                    CREATE TABLE ""__lockstep""
                    (
                        stamp TEXT NOT NULL PRIMARY KEY CHECK(stamp ~ '^[0-9]{14}$'),
                        timestamp TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                ";
                _connection.Execute(script);
            }
        }

        private IList<string> GetExecutedStamps()
        {
            return _connection.Query<string>("SELECT stamp FROM \"__lockstep\" ORDER BY stamp").ToList();
        }

        private IEnumerable<Script> GetScriptsInExecutionOrder()
        {
            string targetStamp = null;
            string targetDirectory = null;
            if (Directory.Exists(_pathSpecification)) {
                targetDirectory = _pathSpecification;
            } else {
                targetStamp = Path.GetFileName(_pathSpecification);
                if (!Script.StampRegex.IsMatch(targetStamp)) throw new Exception($"{targetStamp} is not a valid database revision stamp. Valid stamps have 14 digits and represent a UTC date in the form YYYYMMDDHHmmss.");
                targetDirectory = Path.GetDirectoryName(_pathSpecification);
                if (String.IsNullOrWhiteSpace(targetDirectory)) targetDirectory = ".";
            }
            return Directory.GetFiles(targetDirectory).Select(filepath => Script.FromFile(filepath, _direction)).Where(script => script != null && script.IsRunnableWithTargetStamp(targetStamp)).OrderBy(script => script);
        }

        public void Run()
        {
            try {
                _connection.Open();
                CreateMigrationTableIfNeeded();
                var stamps = GetExecutedStamps();
                var run = false; 
                foreach(var script in GetScriptsInExecutionOrder()) {
                    if (_direction == Direction.Up) {
                        if (!stamps.Contains(script.Stamp)) {
                            Console.WriteLine($"Running {script.FileName}:");
                            var sql = script.Read();
                            Console.WriteLine($"{sql}");
                            _connection.Execute(sql);
                            _connection.Execute("INSERT INTO \"__lockstep\"(stamp) SELECT @stamp", new { script.Stamp });
                            Console.WriteLine($"Completed {script.FileName}:");
                            run = true;
                        } else if (run) {
                            Console.WriteLine($"WARNING: {script.FileName} was previously executed. It was skipped.");
                        }
                    } else {
                        if (stamps.Contains(script.Stamp)) {
                            Console.WriteLine($"Running {script.FileName}:");
                            var sql = script.Read();
                            Console.WriteLine($"{sql}");
                            _connection.Execute(sql);
                            _connection.Execute("DELETE FROM \"__lockstep\" WHERE stamp = @stamp", new { script.Stamp });
                            Console.WriteLine($"Completed {script.FileName}:");
                            run = true;
                        } else if (run) {
                            Console.WriteLine($"WARNING: The up script corresponding to {script.FileName} was not previously executed. {script.FileName} was therefore skipped.");
                        }
                    }
                }

            } catch (Exception e) {
                Console.WriteLine($"{e}");
            }
        }

        public void Diff()
        {
            try {
                _connection.Open();
                CreateMigrationTableIfNeeded();
                var stamps = GetExecutedStamps();
                var scripts = GetScriptsInExecutionOrder();
                var entries = stamps
                                // Find any stamps without corresponding files on disk. 
                                .Except(scripts.Select(script => script.Stamp)).Select(stamp => $"! {stamp}") 
                                // Find any scripts that have not been run.
                                .Union(scripts.Where(script => !stamps.Contains(script.Stamp)).Select(script => $"+ {script.FileName}"))
                                .OrderBy(entry => entry);
                foreach(var entry in entries) {
                    Console.WriteLine(entry);
                }
            } catch (Exception e) {
                Console.WriteLine($"{e}");
            }
        }

        void IDisposable.Dispose()
        {
            _connection.Close();
        }
    }

}