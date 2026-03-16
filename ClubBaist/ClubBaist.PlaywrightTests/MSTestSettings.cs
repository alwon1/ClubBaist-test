// E2E tests share a live database; run test classes in parallel but methods within each class sequentially.
[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]
