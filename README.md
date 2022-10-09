# Gist Dependency Manager for F# interactive

https://user-images.githubusercontent.com/8684875/194775636-daff5b4b-3b26-4658-8d10-eece4017823c.mp4

The Gist dependency manager is now half alive until I discover how to properly distribute and tell others how to use it, in the meantime you can find quick and dirty instructions below

### Quick And Dirty Test

If this is the first time, run:

- `dotnet fsi build.fsx`

after that your project should be within the `dist` directory and can be used as follows

- `dotnet fsi --compilertool:./dist path/to/script.fsx` <- run a script with the dependency manager
- `dotnet fsi --compilertool:./dist` <- run an fsi session with the dependency manager
