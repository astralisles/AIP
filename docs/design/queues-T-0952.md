# Queue and PriorityQueue (T-0952)

`Queue<T>` (std group `queue/`): `queueEmpty`, `queuePush` (to the back), `queueFront`, `queuePop` (returns a new queue), `queueSize`.
`PriorityQueue<T>` (group `priorityQueue/`): `priorityQueuePush(q, item, priority I64)`, `priorityQueueFront`, `priorityQueuePop`,
`priorityQueueSize`, `priorityQueueEmpty`. Lowest priority number comes out first; equal priorities keep insertion order (stable).

Values are immutable like every collection: each change returns a new queue. `Front`/`Pop` on an empty queue fail at run time
(check the size first). Back end: a copied list; the priority queue is a list kept sorted, so push is O(n). A real heap or
persistent structure is the follow-up if profiling shows it matters. Still proposed: Deque, Bag, Bits (see `collection-kinds-T-0951.md`).
Example: `examples/queues`.
