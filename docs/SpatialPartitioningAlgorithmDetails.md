# Implementation of Spatial Partitioning using PCA

## Goal

The goal of the partitioning algorithm is to be able to map each 3D position in a sample to one of a limited set of kinds of regions.  For example, in a sample with two distinct phases, each 3D position might belong to one of the two phases.

There could be any number of ways to perform this partitioning.  The algorithm implemented here is chosen because it can be applied fairly generally, i.e. to many kinds of samples.  

Nevertheless, there are a few user-settable parameters that can tweak the process and thus produce different results.

## Overview

Mapping the results of PCA Analysis to a spatial partitioning should be straightforward.  The sample can be divided into a 3D grid of voxels -- each voxel has an array of values corresponding to some metric.  The PCA math is used to generate a score for each voxel for each of N PCA dimensions.  These scores are then plotted in N dimensional space.  Each partition should correspond to a cluster of values in this N dimensional space. A clustering algorithm can identify which voxels belong to each cluster, and partitions are defined based on the cluster-to-voxel mapping.

There are issues with this approach which made is difficult to implement.  Foremost, it does not scale well for large values of N.  For example, In the case of 5 PCA dimensions, even if the space is divided into only 10 bins per dimension -- which is too few bins to actually distinguish clusters -- there are already 100,000 cells with which to identify clusters. 

As an example, here is a 2D projection of PCA data in a 10 by 10 grid (data actually extends higher and lower, but is put in "overflow bins" for memory conservation).  At higher resolutions, two clusters are distinguishable:

![image](TenByTenGrid.png "PCA data plotted with 10 bins per dimension showing difficulty in distinguishing peaks")

Expanding to 32 bins per dimension, where there is hope of identifying both clusters and clean boundaries between them, means more than 33 million cells, which is memory taxing for our systems.  Additionally, at this point, the statistics of cell population mean that statistical error will be significant in the cluster identification process.

There are ways with which these problems could be tackled, and users wishing to explore N dimensional clustering are encouraged to do so. But we have not chosen to pursue this strategy here.

Instead, The approach here looks at ways to identify clusters using only one or two PCA dimensions at a time. Sometimes, using a single PCA dimensions score is enough to differentiate voxels from different phases.  More often, plotting the PCA scores on two dimensions results in a clearer partitioning.

If the goal is merely to identify two different phases, then a single partitioning is sufficient.  Identifying more than 2 phases with this strategy, however, requires combining the partitions identified in different combinations of PCA data.  That is, the partitions identified in a 2D Plot of PCA dimension 1 and PCA dimension 2 might be overlaid with partitions identified in a 1D plot of PCA dimension 3, resulting in a partitioning matrix.  This matrix is used as the basis for a complete partitioning algorithm.

## Algorithm Outline

1. Generate 1D and 2D plots of the PCA data which show how the voxels might be partitioned
2. Select which partitionings (i.e. which 1D or 2D plots) should be included in the rest of the algorithm
3. For each partitioning, separate voxels into "Group 1", "Group 2", etc., and a "Group ?".  "Group ?" corresponds to those voxels not obviously in any cluster, either because they are in between, or because they are sufficiently far away from the cluster center, or because there are insufficiently many similar voxels with similar PCA scores.  
4. Nomenclature: Each partition is designated with a Letter label. The first partitioning is "A"", the second "B", etc.  Each group within the partitioning has a number, "1", "2", etc., or "?"
5. Using the nomenclature in part 4, designate each voxel with a label for each partitioning.  Example, with three partitions, a voxel which is is in group 1 of all three partitions would be "A1B1C1".  A voxel in group 2 of the first partition and not in any group of the other two would be "A2B?C?"
6. Designate as "core regions" all the voxels that do not have a "?" in their designation. These are the sets of voxels which define the basis for regions that will grow in the successive steps.
7. Identify all voxels that are not in a core region, but are, in real 3D space, adjacent to a voxel in at least one "core region". The particulars of how to determine "Adjacent" are discussed below
8. Evaluate each of the voxels identified in step 7.  If the designation applied in step 5 is similar to the designation of the core region it is adjacent to, add the voxel to that core region.  If the voxel is equally similar to two different adjacent core regions, add it to an "interface voxel" partition -- it will not be added to any core region.
9. If any of the voxels in step 8 have actually been added to a core region, repeat steps 7 and 8 indefinitely.
10. Any remaining voxel which has no assignment different enough from all its adjacent voxels as to fail the test in step 8.  These voxels constitute the "unassigned voxel" partition
11. At this point, all the voxels will either be 
* part of a core region, 
* part of the "interface voxel" partition
* part of the "unassigned voxels" partition 
  
The number of spatial partitions identified will be the number of core regions, plus one (likely) for interfacial voxels, plus one (maybe) for unassigned voxels.

## Identifying Partitions in One Dimensional Data

## Identifying Partitions in Two Dimensional Data

## Identifying Voxels for Consideration when Growing the Core Regions

## Evaluating Voxels Similarity when Growing the Core Regions


## Why Use Concentrations as Input for PCA

One appeal of PCA analysis is that is is agnostic to the meaning of the input data. This implementation uses the concentrations of identified elements in each voxel as input for the calculation, but there is no reason why other data could not be fed in as well. Raw peak counts, for example, could be used, requiring no mapping of peak to ion.  The total population of ions in each voxel could be used, or the number of each identified ion.

PCA is fundamentally a "dimension reduction technique" -- i.e. if P dimensions of data per voxel are fed into the calculation, PCA will produce P - Q dimensions of data that are most relevant to differentiating the voxels. There is fundamentally no penalty for using additional data.

However, PCA analysis is also agnostic to the meaning of the data it produces, and it is likely that it will generate scores based on trends in the data that are unwanted for the purposes of phase identification.  

For example, we know that because the voltage increases during an APT run, the mix of single and double charged ions will also change over the during the run.  If we were to use raw peak counts, part of the scores generated by PCA would be differentiating voxels based on when during the run the data for that voxel was collected.  And, most problematically, it might be hard to tell how that data is captured in the PCA results.

Similarly, using any measure directly associated with the density of the sample would produce part of the PCA results that reflected voxels near the sample surface, where the "density" is affected because part of the voxel is expected to be vacuum.

Using only concentration of the voxels is a good way to shield the PCA analysis from identifying trends in the data that we already know about and we already know do not contribute to the goal of phase identification.




