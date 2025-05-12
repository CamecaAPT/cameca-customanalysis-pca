## About the Phase Identification Algorithm 

PCA analysis is best understood as a 'dimension-reduction' technique in analysis of large datasets.  That is, in looking through a large amount of data collected from a large number of sources, it is often the case that much of the differences in the data collected from each source can be attributed to a smaller number of factors.  Partitioning the sources into subsets based on these identified factors is one way of making sense of the data.

In atom probe datasets, one goal of PCA analysis is to partition a sample into different regions based on the data collected.  This case be based on the compositions measured in different parts of a sample after ions are assigned to chemical identities based on mass ranges, or based on raw charge/mass ratio data. 

The output of the PCA analysis done here of APT data is an array of 'scores' for each voxel in the dataset, one score for each "PCA dimension".

In the simplest and most boring case, all voxels in the sample are identical, and the are exactly zero PCA dimensions of interest. The entire sample is a single homogenous region.

In an ideal two phase sample, the will be a predominance of voxels of two different types, and a PCA analysis will identify a single dimension of interest.  Voxels of the two types will have "scores" on this dimension clustered around two different values.  Identifying the two different phases of the sample is just identifying regions with voxels associated with each cluster.  Voxels in between the clusters could correspond to interface regions. 

## Overview of the Case of a Single PCA Dimension

The algorithm used here would treat a ideal two-phase material as follows:

PCA dimensions are given a letter code starting with A, so each cluster of voxels on this dimension includes an A.  There will be two clusters, A1 and A2. Each voxel in each cluster is assigned that code. Voxels not in the cluster are assigned the designation A0. 

### Part 1) Mapping a projection

To assign voxels to designations, The scores of all the voxels are mapped onto a one dimensional projection. The data is modeled as an array of bins, like a histogram, except that each voxel contributes to multiple bins, so as to have a constant delocalization.  That is, if the buckets are of binsize 1, centered on each integer value, a voxel with a score of 0.5 contributes equally to the bin at 0 and the bin at 1. The delocalization produced by splitting the voxel between the two bins is measured by 

    D = Sum[n] ( Cn * DXn^2 ) 
    
That is, the Sum over the contributions to the different n bins, where Cn is the contribution to the nth bin, and DXn is the distance between the actual score and the bin center.

For the simple case above, the voxel contributes half to each of bin 0 and bin 1, so C0 is 0.5 and C1 is 0.5.  The distance between the voxel score and bin 0 position is -0.5 (DX0 = -0.5), and to the bin 1 position it is 0.5 (DX1 = 0.5).

    D0 = 0.5 * (-0.5)^2  = 0.125     
    D1 = 0.5 *    0.5^2  = 0.125
    
    D  = D0 + D1         = 0.25
    
Note that if the voxel contributed 100% to either bin 0 or bin 1, the delocalization would still be 0.25 -- splitting between the bins does not increase the delocalization but it does that the weight of the score is represented in the projection at the right place  -- i.e. it is not moved up or down along the line.

In order to maintain a 0.25 delocalization for all points, even voxels with a score at exactly the bin center make contributions to neighboring bins. A voxel with a score of 1 makes contributions to the bins at 0 and 2 of 0.125 and a contribution to the bin at 1 of the remaining 0.75.  The delocalizations are

    D0 = 0.125 * (-1)^2  = 0.125     
    D1 = 0.75  *    0^2  = 0
    D2 = 0.125 *    1^2  = 0.125     

    D  = D0 + D1 + D2    = 0.25
    
The result of mapping all the voxels onto a projection in this way is an array of bins.  The clusters are represented by peaks in the graph of the bin value v. bin population.

There are two extra parameters involved in the generation of the projection:  the binsize and the delocalization distance.  Looking carefully at the equations above, the binsize is in units of PCA score, and the delocalization D is in units of PCA score squared. And, if the binsize is doubled, the delocalization is quadrupled -- the delocalization scales as the square of the binsize, that is

     D = 0.25 B^2

Imagining the PCA score to be a "distance", the Delocalization can be expressed in the same units by taking the square root of the Delocalization (The "Delocalization distance") in which case the Delocalization Distance scales linearly with the binsize

     DD = 0.5 * B
     
So, the two extra parameters are Binsize and Delocalization Distance.  Specifying a Delocalization Distance of less than half the binsize does nothing -- generating the projection implies a Delocalization Distance of Binsize * 0.5.  Specifying a larger Delocalization Distance, however, means that the a projection will be generated by smoothing the voxels out over a larger distance. Depending on the sample  (and the chosen binsize), this may improve the association of voxels with their appropriate cluster.

### Part 2) Partitioning the projection

At this point, there is a One dimensional array of values, and ideally, plotting the values vs. the bin positions shows two peaks corresponding to clusters of voxels, which represent the two phases of the sample.
 
To identify which bins are part of which peak, the algorithm finds the most populated bin of all the bins, and then accumulates all the adjacent bins into a group, using the following rules:

Starting with the maximum bin, it looks at each adjacent bin, and adds it to the group if A) that adjacent bin has a lower value than it (the reference bin), but higher than a "noise floor" value. or B) if that adjacent bin has a higher value than some fraction of the peak maximum, say 75%. This fraction is referred to in the UI as "Peak Summit Allowance"

Case A is meant to capture the usual case -- the values are likely to decrease continually until the peak reaches a noise level. If there is a second peak that occurs before the peak reaches a noise floor, the value will be increasing. Case B account for the fact that near the peak maximum, there may be noise such that the value near the peak might be 

         82, 95, 100, 98, 99, 92, 78, ...
         
In this case, the algorithm starts at 100, and identifies that both the 95 and 98 bins are part of the peak. Then the algorithm finds the 99 bin.  Rule A on its own would not add the 99 bin to the peak, as it would trigger the logic of finding a second peak.  However, as the 99 value is more than the Peak Summit Allowance, this bin is considered to be part of the peak, and the algorithm would continue in evaluating the 82 and 92 bins, and so forth.

In the case that a bin is found which is an increase from the previous bin added to the peak and lower than the Peak Summit Allowance, the algorithm then know that there must be an additional peak -- It looks over all the bins and finds a maximum value in the remaining bins, and starts again to identify bins that are part of the peak.  The bin which has a neighbor on both side with a greater value is not considered to be part of either peak.

### Part 3) Initial Voxel Assignment

After peak identification, the result is that some bins are assigned to the each of the peaks, where as some bins are assigned to no peak at all - those bins which are below the noise floor or at the border between two peaks. Then for each voxel, the corresponding bin is located, and if it is part of the first peak, it gets a designation A1, if part of the second peak, A2, etc.  A designation of A0 is assigned for voxels corresponding to bins not in any peak.

From these initial designations, the algorithm creates collections of voxels corresponding to the "PCA Phase" corresponding to each peak-related designation.  If there are two peaks, there will be an A1 phase and an A2 phase.

### Part 4) Voxel Additions

At this point, there are also a collection of voxels with the designation A0 that do not belong to either phase.  This part of the algorithm attempts to assign them to one of the existing phases.  The logic proceeds like this:

For each voxel, the neighboring 6 voxels sharing a face with it are examined.  If one or more of these neighbors are part of one (but only one) of the PCA Phases, this voxel is added to the collection of voxels in that phase.  If there are neighbors in multiple PCA phases, this voxel is added to a separate collection of "Interface Voxels"

This step is applied iteratively -- as voxels are added to each phase, new additions can be made possible.  At some point, no further Voxel additions are possible -- all voxels have been assigned to a phase or are ineligible to be assigned (In this simple example, it would be because an unassigned voxel only has neighbors that are interface voxels, but in more complex samples, there will be additional cases)

### Part 5) ROI creation

After the collections of voxels are made for each PCA phase, the set of voxels can be exported as an "ROI" in IVAS.  An additional parameter controls which phase is exported.

The PCA phases are ordered in terms of number of voxels, the phase with the most voxels has the index 1, the second most, index 2, etc.  If index 0 is chosen, an ROI is generated containing the voxels which are unassigned.  The last index (number of PCA phases identified +1) will export an ROI consisting of all the identified interface voxels.

Note that an ROI can also be generated based on pure PCA scores.  In this case, there is no guarantee that a particular voxel is assigned to only one region. The currently implemented UI offers both possibilities -- ROI bases on PCA score or by the PCA Phase identification process.  The checkbox in the PCA properties panel "Use PCA Phase for Detatched ROI" toggles between these two options. If the checkbox is unchecked, ROI creation is based on the PCA score identified by the "Component Index" box -- a value of 0 here indicates the first PCA component (i.e. it is zero-indexed).

## Extending the algorithm to multiple PCA dimensions.

The strategy described above scales particularly well to incorporating multiple PCA dimensions, as will occur for almost any APT sample.  The basic outline remains the same:

1) Based on PCA scores, partition voxels into different clusters, and apply a PCA code (such as A1, A2, A0) for each cluster to the voxels

2) Define "PCA Phases" based on the PCA codes(s) applied to each voxel

3) Add Voxels to the PCA phases defined in step 2

4) Generate ROIs

There are, however, a number of areas where the algorithm becomes more complex.  Among these:

### Use of 2D projections

In the case where multiple PCA dimensions of interest are deteremined, it is useful to use a 2D projections of the PCA score data, as a way to better distinguish between clusters.  That is, instead of using a 1D projection of PCA score data onto a 1D histogram, We can use a projection of 2 dimensions of PCA scores.

If the data were only projected onto 1 dimensional axes the peaks would significantly overlap.  Using projections onto 2 dimensions improves the partitioning.

When using 2 dimensional projections, the peak identification algorithm operates the same way, the only difference being that a 2D grid of bins (pixels) is generated instead of an array of bins, applying delocalization in two dimensions during grid generation, a peak is made of a collection of pixels instead of bins, neighbors are considered in two dimensions rather than one, and the PCA code associated with clusters takes on letters from both dimensions.  That is, the clusters identified from a projection onto the first two PCA dimensions would be AB1, AB2, etc. Voxels not in a cluster on this grid are designated AB0.

### Use of Multiple projections

When combining PCA codes from multiple projections, all projections contribute to the code corresponding to a "PCA Phase".  That is, if data is taken from 3 PCA dimensions, and a 2 dimensional projection is used for directions 1 and 2, the different PCA phases that result might be

    AB1 C1
    AB2 C1
    AB1 C2
    AB2 C2
    
It may seem like there is a significant risk of an explosion in the number of phases as the number of PCA dimensions increases, but in practice this does not seem to be an issue.
    
### Use of long tails

It is observed that it is not always possible to identify "peaks" is the projections that correspond to meaningful partitions in the distributions.  This is be caused by both A) low populations -- in the case where one phase has a low enough voxel count, the voxels may be hard to distinguish from a noise level.  and B) broad distributions -- PCA scores may form a broad enough cluster that similar voxels do not form recognizable peaks.   

In this case, it can be enough to identify a single peak in the PCA scores, and degnate any voxels placed far away from the peak as belonging to a different phase.  So, if this were applied to the fourth PVCA dimension, D1 would designate voxels inside the peak, D2 would designate voxels well outside the peak, and D0 would designate voxels close to but not in the peak.

 





