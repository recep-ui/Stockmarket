using Xunit;

// Disable parallel execution across integration test classes to prevent SQLite file lock collisions
[assembly: CollectionBehavior(DisableTestParallelization = true)]
