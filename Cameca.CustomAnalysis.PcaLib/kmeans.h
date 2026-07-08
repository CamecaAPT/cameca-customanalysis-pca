#ifndef __KMEANS_H__
#define __KMEANS_H__

#include "blas_wrapper.h"
#include "utilities.h"
#include <numeric>
#include <limits>
#include <optional>
#include <algorithm>
#include <Eigen/Dense>

using namespace Eigen;

template <typename T> using VectorT = Matrix<T, Dynamic,1>;
template <typename T> using MatrixT = Matrix<T, Dynamic, Dynamic>;

// Construct an empirical cdf
template <typename T>
void normcumsum(VectorT<T> &v)
{
    std::partial_sum(v.begin(),v.end(),v.begin());
    v /= v(v.size()-1);
    v(v.size()-1) = (T)1.0;  // make sure maximum is exactly 1
}

// pick a random index with weight proportional to the squared distance
template <typename T>
int randsample(VectorT<T> &dist) {
    T p = rand<T>(0,1);  // p in interval [0, 1), always < 1
    normcumsum(dist); // last element is exactly 1
    auto it = std::lower_bound(dist.begin(),dist.end(),p); // never equals dist.end()
    return std::distance(dist.begin(), it);
}

// Cost is the sum of the squared distances from a point to its cluster centroid
template <typename T>
T costFcn(const VectorT<T> &minVal) {
    return minVal.sum();
}

// dist is nSamples x nComponents, the squared distance from each point to each centroid
// If the optional diagCV is provided, the squared mahalnobis distance is computed
// Note diagCV.value() has type Eigen::VectorT<T>
template <typename T, typename derived>
void distance(const MatrixT<T> &C, const MatrixBase<derived> &X, MatrixT<T> &dist,
              std::optional<VectorT<T>> diagCV = std::nullopt) {
        for (auto i = 0; i < C.cols(); i++) {
            if(diagCV)
            dist.col(i) = ((X.colwise() - C.col(i)).array().colwise() / diagCV.value().array()).matrix().colwise().squaredNorm().transpose();
            else
                dist.col(i) = (X.colwise()-C.col(i)).colwise().squaredNorm().transpose();
        }
}

// compute squared euclidean dist between vector C and each column of matrix X
// If the optional diagCV is provided, the squared mahalnobis distance is computed
template <typename T, typename derived>
void distance(const VectorT<T> &C, const MatrixBase<derived> &X, VectorT<T> &dist,
              std::optional<VectorT<T>> diagCV = std::nullopt) {
    if (diagCV)
        dist.col(0) = ((X.colwise() - C).array().colwise() / diagCV.value().array()).matrix().colwise().squaredNorm().transpose();
    else
        dist = (X.colwise() - C).colwise().squaredNorm().transpose();
}

// K-means++ algorithm to initialize k-means
template <typename T, typename derived>
VectorXi kmeanspp_init(MatrixT<T> &C, const MatrixBase<derived> &X,
                       std::optional<VectorT<T>> diagCV = std::nullopt) {
    int nClust = C.cols();
    int nSamples = X.cols();
    int nFeatures = X.rows();
    int nextIndex;
    VectorXi idxClust = VectorXi::Constant(nClust,-1);
    VectorT<T> minDist = VectorT<T>::Constant(nSamples,std::numeric_limits<T>::infinity());
    VectorT<T> dist = VectorT<T>::Zero(nSamples);
    VectorT<T> aCol(nFeatures);

    // Randomly choose the first centroid
    idxClust(0) = rand<int>(0, nSamples-1);
    aCol = X.col(idxClust(0));
    C.col(0) = aCol;

    // Use k-means++ approach to select remaining centroids
    for (int i = 1; i < nClust; i++) {
        distance(aCol, X, dist, diagCV);  // distance from index[i-1]
        minDist = minDist.cwiseMin(dist);  // update the min distance coefficient-wise
        dist = minDist;
        // Make sure next index is unique
        while (idxClust(i) < 0) {
            nextIndex = randsample(dist);
            if (!((idxClust.array() == nextIndex).any()))
                idxClust(i) = nextIndex;
        }

        aCol = X.col(idxClust(i));
        C.col(i) = aCol;
    }
    return idxClust;
}

template <typename T, typename derived>
T updateCentroid(MatrixT<T> &C, const MatrixBase<derived> &X, VectorXi &indexVector,
                 std::optional<VectorT<T>> diagCV = std::nullopt) {
    // returns the cost
    // m is the number of features, n the number of clusters, k the number of samples
    int m = C.rows(), n = C.cols(), k = X.cols();

    // Allocate memory for the scaling matrix
    MatrixT<T> SM = MatrixT<T>::Zero(k,n);
    MatrixT<T> D = MatrixT<T>::Zero(k,n);
    VectorT<T> minVal = VectorT<T>::Zero(k);

    VectorXi indexCount;
    VectorT<T> distvec;
    bool done = false;
    int minIndex, maxIndex, maxClusterIndex, zeroClusterIndex;

    // Compute the distances from each col of C to each column of X
    int maxTries = 5, tries = 0;
    do {
        indexCount = VectorXi::Zero(n);
        distance(C, X, D, diagCV);

        // Find the closest centroid from each col of X and construct SM
        SM.setZero(k,n);  // nSamples x nClust
        for (int i = 0; i < k; i++) {
            minVal(i) = D.row(i).array().minCoeff(&minIndex);
            indexVector(i) = minIndex;
            indexCount(minIndex)++;
            SM(i,minIndex) = 1.0;
        }
        // Take care of case that no points are assigned to a centroid
        // not really tested
        done =(indexCount.array()>0).all();
        if (!done) {
            // find the index of a zero cluster and the largest cluster
            indexCount.array().minCoeff(&zeroClusterIndex);
            indexCount.array().maxCoeff(&maxClusterIndex);
            // find a random sample index far from the largest cluster centroid
            distvec = D.col(maxClusterIndex);
            maxIndex = randsample(distvec);
            // D.col(maxClusterIndex).array().maxCoeff(&maxIndex);
            // replace the centroid
            C.col(zeroClusterIndex) = X.col(maxIndex);
            // print_matrix(C);
        }
    } while(!done && ++tries < maxTries);

    T totalCost = costFcn(minVal);

    // Normalize SM to sum to one and reorder by cluster size and update C
    // This is a problem if a cluster count goes to zero
    VectorT<T> clustCount = SM.colwise().template lpNorm<1>();
    SM.array().rowwise() /= clustCount.array().transpose();

    if (!isSortedDescending(clustCount)) {
        VectorXi clustIndex(n);
        std::iota(clustIndex.begin(),clustIndex.end(),0);
        std::stable_sort(clustIndex.begin(), clustIndex.end(),
                         [&clustCount](int i1, int i2) {return clustCount[i1]>clustCount[i2];});
        PermutationMatrix<Dynamic,Dynamic> perm(clustIndex);
        SM = SM*perm.transpose();
    }

    // Reestimate C = X*SM
    blas_product_AB(m,n,k,X.derived().data(),SM.data(),C.data());

    // Return the total cost based on the INPUT C
    // That is, on the ith call, returns cost of the (i-1) iteration
    return totalCost;
}

template <typename derived, typename derivedI,
         typename T = typename MatrixBase<derived>::Scalar >
T trainModel(const MatrixBase<derived> &X, const int nClust, const int nReplicates,
                 MatrixBase<derived> &Centroid, MatrixBase<derivedI> &clustID, bool doWeighting=true)
{
    int nSamples = X.cols();
    int nFeatures = X.rows();
    T thiscost;
    int maxIter = 200, iter;
    VectorXi idxClust(nSamples);   // Cluster labels assigned to each column of X
    VectorXi oldIdxClust;
    MatrixT<T> Cntr(nFeatures,nClust);  // Cluster centroids
    VectorXi idxSample;  // randomly chosen initial rows of Cntr
    T cost = std::numeric_limits<T>::infinity();

    // Get the covariance vector = mean spectrum, if desired
    std::optional<VectorT<T>> diagCV = getDiagCV(X,doWeighting);
    // We need the scaling vector sqrt(diagCV)
    if (diagCV)
        diagCV.value() = diagCV.value().array().sqrt();


    // Perform nReplicates and return the lowest cost clustering

    for (int rep = 0; rep < nReplicates; rep++) {

        // Intialize Cntr with rows of X chosen by k-means++
        idxSample = kmeanspp_init(Cntr,X,diagCV);  // idxSample is not used

        // Do k-means iterations
        idxClust.setZero();
        oldIdxClust = idxClust;
        for (iter = 0; iter < maxIter; iter++) {
            thiscost = updateCentroid(Cntr, X, idxClust,diagCV);
            if (idxClust.isApprox(oldIdxClust)) {
                // printf("%d iterations, cost: %g\n\n",iter,thiscost);
                break;
            }
            else
                oldIdxClust = idxClust;
        }
        if (iter == maxIter) {
            // printf("Maximum iteration count reached\n\n");
        }

        // keep the best clustering and assign to outputs
        if (thiscost < cost) {
            cost = thiscost;
            Centroid = Cntr;
            clustID = idxClust;
        }
    }  // replicates
    return cost;
}

// Overloaded function for analyzing PCA representation of data
template <typename derived, typename derivedI,
typename T = typename MatrixBase<derived>::Scalar >
    T trainModel(const MatrixBase<derived> &scores, const MatrixBase<derived> &loadings, const int nClust, const int nReplicates,
               MatrixBase<derived> &Centroid, MatrixBase<derivedI> &clustID, bool doWeighting=false)
{
    Matrix<T,Dynamic,Dynamic> centroidsOfPCs = MatrixT<T>::Zero(scores.cols(), nClust);
    Matrix<T,Dynamic,Dynamic> scoresT = scores.transpose();

    T cost = trainModel(scoresT, nClust, nReplicates, centroidsOfPCs, clustID, doWeighting);
    Centroid = loadings*centroidsOfPCs; // Centroids in the data space
    // Centroid = loadings*centroidsOfPCs.completeOrthogonalDecomposition().pseudoInverse().transpose(); // Centroids in the data space
    return cost;
}

#endif /* __KMEANS_H__ */