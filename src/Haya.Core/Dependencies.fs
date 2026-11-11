namespace Haya.Core.Analysis

/// A type declared in the analysed source (class, struct, record or interface): a node of the type graph that the
/// dependencies between annotated classes are walked over (ADR018). Types from the framework, packages or referenced
/// assemblies are never nodes.
type TypeNode = {
    /// Unique within one analysis. Only used to join nodes and edges; never written out.
    Key: string
    Name: string
    /// Where the type is, as for a component (relative to the analysed root).
    Source: string
    /// An interface or abstract class: reaching it by use also reaches the types that implement or extend it.
    Abstract: bool
}

/// Code in `From` refers to `To`. `Inherits` when `To` is a base type or an implemented interface of `From`.
type TypeEdge = { From: string; To: string; Inherits: bool }

/// The walk from each annotated class to the annotated classes it reaches (ADR018). Both analysers only build the type
/// graph; the rule is here, once, so C# and TypeScript give the same answer.
module Dependencies =

    /// Fill the `Dependencies` of each component from the type graph. A component is the node with its name and source.
    /// - Breadth-first from the component, through unannotated types, stopping at the first annotated class on each
    ///   path: that class is recorded, with the unannotated type names in between (`Via`), and is not walked past.
    /// - Reaching an abstract type (interface or abstract class) by use also reaches each type that inherits from it.
    ///   Reaching it by inheritance does not, so two implementations of one interface are not linked to each other.
    /// - Edges to unknown types and self edges are dropped; cycles are walked once; never a dependency on itself.
    /// - Neighbours are visited by name, source, key, so the shortest chain and its tie-break are deterministic.
    let attach (nodes: TypeNode list) (edges: TypeEdge list) (components: ComponentDetail list) : ComponentDetail list =
        let nodes = nodes |> List.distinctBy (_.Key)
        let byKey = nodes |> List.map (fun n -> n.Key, n) |> Map.ofList
        let order (n: TypeNode) = n.Name, n.Source, n.Key
        let annotated = components |> List.map (fun c -> c.Name, c.Source) |> Set.ofList
        let isAnnotated (n: TypeNode) = annotated.Contains (n.Name, n.Source)
        let edges =
            edges
            |> List.filter (fun e -> e.From <> e.To && byKey.ContainsKey e.From && byKey.ContainsKey e.To)
            |> List.distinct
        let outgoing =
            edges
            |> List.groupBy (_.From)
            |> List.map (fun (k, es) -> k, es |> List.map (fun e -> byKey[e.To], e.Inherits) |> List.sortBy (fun (n, i) -> order n, i))
            |> Map.ofList
        let inheritors =
            edges
            |> List.filter (_.Inherits)
            |> List.groupBy (_.To)
            |> List.map (fun (k, es) -> k, es |> List.map (fun e -> byKey[e.From]) |> List.distinct |> List.sortBy order)
            |> Map.ofList
        let find key (m: Map<string, 'a list>) = m |> Map.tryFind key |> Option.defaultValue []

        // the nodes one step on, each with whether it was reached by use (and so may fan out to its inheritors)
        let neighbours (n: TypeNode) (byUse: bool) =
            [ for (m, inherits) in find n.Key outgoing do yield m, not inherits
              if n.Abstract && byUse then
                  for m in find n.Key inheritors do yield m, true ]

        let reach (start: TypeNode) : ComponentDependency list =
            let visited = ref (Set.ofList [ start.Key, true ])
            let reached = ref Set.empty
            let found = ResizeArray<ComponentDependency>()
            // each entry: the node, reached by use, and the unannotated names from the start up to and including it (reversed)
            let mutable frontier = [ start, true, [] ]
            while not frontier.IsEmpty do
                let next = ResizeArray()
                for (n, byUse, via) in frontier do
                    for (m, mByUse) in neighbours n byUse do
                        if m.Key <> start.Key && not (visited.Value.Contains (m.Key, mByUse)) then
                            visited.Value <- visited.Value.Add (m.Key, mByUse)
                            if isAnnotated m then
                                if not (reached.Value.Contains m.Key) then
                                    reached.Value <- reached.Value.Add m.Key
                                    found.Add { Name = m.Name; Source = m.Source; Via = List.rev via }
                            else next.Add (m, mByUse, m.Name :: via)
                frontier <- List.ofSeq next
            List.ofSeq found

        components
        |> List.map (fun c ->
            let starts = nodes |> List.filter (fun n -> n.Name = c.Name && n.Source = c.Source)
            if starts.IsEmpty then c else
            { c with
                Dependencies =
                    starts
                    |> List.collect reach
                    |> List.filter (fun d -> not (d.Name = c.Name && d.Source = c.Source))
                    |> List.distinctBy (fun d -> d.Name, d.Source) }
            |> ComponentDetail.normalise)
