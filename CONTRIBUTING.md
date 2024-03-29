Contributor Guide
=================

Prerequisites
-------------
To work with the project, you'll need [.NET SDK 8][dotnet-sdk] or later.

Build
-----
Use the following shell command:

```console
$ dotnet build
```

Pack
----
To prepare a package, use the following shell command:

```console
$ dotnet pack
```

GitHub Actions
--------------
If you want to update the GitHub Actions used in the project, edit the file that generated them: `scripts/github-actions.fsx`.

Then run the following shell command:
```console
$ dotnet fsi scripts/github-actions.fsx
```

[dotnet-sdk]: https://dotnet.microsoft.com/en-us/download
