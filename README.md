# Datus Migrator

Datus Migrator is a simple tool for creating, executing, and rolling back hand-coded PostgreSQL migrations.

## Building And Running

Datus Migrator can be run directly from the project folder using `dotnet run` followed by the appropriate sub-command, e.g.,

```bash
dotnet run up "host=localhost;database=awesome" ./db
dotnet run down "host=localhost;database=awesome" ./db/20191029003014
```

Datus Migrator can also be built as a command-line executable for specific platforms. For this, a .NET Core Runtime Identifier (RID) is required. Microsoft lists RIDs [here](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog). 

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Building and deploying .NET Core console applications is outside the scope of this README. Please refer to the appropriate documentation.

The resulting executable can then be renamed to whatever one wishes and placed in the path. For the purposes of the remainder of this README, it is assumed that an executable with the name `migrate` has been built, e.g.,

```bash
# Performs a migration against the specified database, and looks for
# scripts in the current directory.
migrate up "host=localhost;database=awesome"

# Performs a down-migration agaist the specified database, looking for
# scripts in the ./db directory.
migrate down "host=localhost;database=awesome" ./db
```

If you prefer to run Datus Migrator from the project folder, just substitute `dotnet run` for `migrate`.

## Configuration

Datus Migrator requires no configuration. Instead, arguments are passed on the command line. For the `up`, `down` and `diff` commands, there are only two arguments: the connection string of the targeted database and, optionally, a _path specification_, which contains both a directory in which to look for migrations and optionally a _target stamp_, which will be explained later in this README. 

## Migration Files

Migrations are stored in script files with the extension `.psql`. Each migration consists of two files: an "up" and a "down". The "up" migration makes some change to the database and the corresponding "down" migration fully reverses this change. Migrations use a highly specific naming convention, consisting of a 14-digit UTC timestamp, followed by `.up` or `.down`, followed optionally by a descriptive name beginning with a `.`, and lastly ending with `.psql`. For example:

```
20191029012033.up.create-table-foobar.psql
20191029012033.down.create-table-foobar.psql
```

## Creating Migration Files

The easiest and safest way to create a pair of up and down migration files with the proper UTC timestamp is to use the `new` sub-command, e.g.

```bash
migrate new ./rsrc/create-table-foobar ./rsrc/create-table-fizzbuzz
```

This command creates the corresponding up and down migration files in the `./rsrc` folder using UTC timestamps. The example above creates _four_ files on disk, two for each argument to the `new` subcommand, corresponding to the timestamped "up" and "down" migrations.

## Migrating

Datus Migrator performs migrations using the `up` sub-command, which takes two arguments: the connection string of the targeted database and optionally a path specification, which defaults to the current directory. Let's look at an example:

```bash
migrate up "host=localhost;database=awesome" ./rsrc/20191030172111
```

The first argument to `up` is obviously the connection string. The second argument requires some explanation. The string `20191030172111` does not represent a filename. Instead, it represents the UTC timestamp of the revision we're targeting. This revision is called the _target stamp_. _It does not actually have to exist._ That is, no filename with the given revision timestamp needs to exist on disk. Datus Migrator will run all revisions that have not previously run up to _and including_ the specified target stamp. 

The migrator looks for migrations in the `./rsrc` folder. If this folder does not exist, Datus Migrator exits with an error.

It is not necessary to specify a target timestamp:

```bash
migrate up "host=localhost;database=awesome" ./rsrc
```

This will run all migrations in the `./rsrc` folder which have not previously been run. For up migrations, this is the most common case.

## Rolling Migrations Back

Datus Migrator can roll migrations back using the `down` command. The syntax is identical to `up` except for the use of the `down` sub-command:

```bash
migrate down "host=localhost;database=awesome" ./rsrc/20191030172111
```

See the section on migrating to understand this syntax. It should be noted that down migrations should almost always be run with a target stamp, while for up migrations this is rare.

## An Important Note On Target Stamps

Whether performing an up or a down migration, Datus Migrator's goal is to get the database into the state identified by the target stamp. This means that for an up migration, the migration identified by the target stamp (if any) will be run, so that after migration, that is the latest stamp. For a down migration, all migrations down to _but not including_ the target stamp will be run, so that after migration, that is the latest stamp.

## Diffing

The `diff` sub-command compares the state of the database with the files on disk.

```bash
migrate diff "host=localhost;database=awesome" ./rsrc
```

If the database and the files on disk are not in sync, it produces two-column output similar to the following:

```
! 20190920041321
+ 20191030172111.up.create-table-foobar.psql
+ 20191101140220.up.alter-table-foobar-add-column-watusi.psql
```

The `+` symbol means that the given file has not yet been run. Calling the `up` subcommand will run the given files. Hopefully you will never see `!`. This means that a stamp exists in the database _which has no corresponding file on disk_. This represents a serious error. Correcting it will be a matter of proper investigation, but in all cases it means that a file on disk was deleted. This usually occurs when a migration in a git branch was executed but then the resulting branch was not merged.

If `diff` produces no output, then the database and the files on disk are fully in sync.