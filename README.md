# Gist Dependency Manager for F# interactive

<video src="./gist-dep-man-2.mp4">

The Gist dependency manager is now half alive until I discover how to properly distribute and tell others how to use it, in the meantime you can find quick and dirty instructions below

### Quick And Dirty Test

- `dotnet publish -o dist`
- `dotnet fsi --compilertool:./dist test.fsx`
