namespace Lib;

// TODO: We deviate a bit from CRoaring here as we don't have array/run at the moment
public readonly record struct Statistics(uint ContainerCount, uint SortedSetContainerCount, uint BitsetContainerCount,
    uint SortedSetContainerValues, uint BitsetContainerValues, uint SortedSetContainerBytes, uint BitsetContainerBytes,
    uint MinValue, uint MaxValue, ulong ValueSum, ulong Cardinality);
