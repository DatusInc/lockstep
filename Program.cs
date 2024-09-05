namespace Datus.LockStep
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    class Program
    {
        static readonly IDictionary<string, Action<string[]>> handlers = new Dictionary<string, Action<string[]>>();

        static Program()
        {
            handlers["diff"] = HandleDiff;
            handlers["down"] = HandleDown;
            handlers["new"] = HandleNew;
            handlers["up"] = HandleUp;
        }

        static void Main(string[] args)
        {
            if (args.Length == 0) {
                ShowUsage();
            } else {
                if (handlers.TryGetValue(args[0].ToLower(), out var handler)) {
                    handler(args.Skip(1).ToArray());
                } else {
                    ShowUsage();
                }
            }
        }

        static void ShowUsage()
        {
            // TODO: Show usage here.
        }

        static void HandleRun(string[] args, Direction direction)
        {
            if (args.Length > 0) {
                var connectionString = args[0];
                var pathSpecification = args.Skip(1).FirstOrDefault() ?? ".";
                using (var runner = new ScriptRunner(connectionString, pathSpecification, direction))
                {
                    runner.Run();
                }
            } else {
                ShowUsage();
            }
        }

        static void HandleUp(string[] args)
        {
            HandleRun(args, Direction.Up);
        }

        static void HandleDown(string[] args)
        {
            HandleRun(args, Direction.Down);
        }

        static void HandleDiff(string[] args)
        {
            if (args.Length > 0) {
                var connectionString = args[0];
                var pathSpecification = args.Skip(1).FirstOrDefault() ?? ".";
                using (var runner = new ScriptRunner(connectionString, pathSpecification, Direction.Up))
                {
                    runner.Diff();
                }
            } else {
                ShowUsage();
            }
        }

        static void HandleNew(string[] args)
        {
            foreach(var arg in args) {
                var directory = Path.GetDirectoryName(arg);
                if (String.IsNullOrWhiteSpace(directory)) directory = ".";
                if (!Directory.Exists(directory)) {
                    Console.WriteLine($"The directory {directory} does not exist. Skipping {arg}.");
                    continue;
                }
                var description = Path.GetFileName(arg);
                var now = DateTime.UtcNow;
                foreach (var direction in new string[] { "up", "down" }) {
                    var name = String.Format("{0:yyyyMMddHHmmss}.{1}.{2}.psql", now, direction, description);
                    var filepath = Path.Combine(directory, name);
                    File.WriteAllText(filepath, "");
                    Console.WriteLine($"Created {filepath}.");
                }
            }
        }
    }
}
