#r "gist: b9e9efa19b7c89700c422c0ee5548edb"
#r "gist: not a gist id, not a revision"

open MyTypes
open MyFunctions
let a: MyType = { name = "a" }

printfn "Running Stuff"
printfn $"MyType %A{a}"

printValue $"{add 123 10}"
