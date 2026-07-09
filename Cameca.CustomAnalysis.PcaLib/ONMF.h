#ifndef __ONMF_H__
#define __ONMF_H__

#include "utilities.h"
#include <numeric>
#include <limits>
#include <optional>
#include <algorithm>
#include <Eigen/Dense>

// ONMF implements Orthogonal Non-negative Matrix Factorization
// It can be used with either the full dataset, or a 2-matrix PCA reprentation
// By default the input data are implicitly root-mean scaled to account for 
// counting statisticsThe vector:
//
// std::optional<Vector<T,Dynamic>> diagCV = std::nullopt
// If scaling, diagCV.value() is the mean spectrum, otherwise nullopt
//
// For the PCA representation, input X becomes the scores matrix, and the loadings are:
// std::optional<Matrix<T,Dynamic,Dynamic>> loadings
// if the full data set is used, loadings.value() = std::nullopt

using namespace Eigen;

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void initializeEM(const MatrixBase<derived> &X, const std::optional<Matrix<T,Dynamic,Dynamic>> loadings,
                  Matrix<T,Dynamic,Dynamic> &H, Matrix<T,Dynamic,Dynamic> &W)
{
    std::uniform_real_distribution<T> dist(0,1);
    fillRandom(H, dist);
    // Only keep the maximum value in each row
    Index max_index;
    T max_val;
    for (auto i = 0; i < H.rows(); i++) {
        max_val = H.row(i).maxCoeff(&max_index);
        H.row(i).setZero();
        H.row(i)(max_index) = max_val;
    }
    // Make H orthonomal
    normalize(H,2);

    if (loadings.has_value())
        W = loadings.value()*(X.transpose()*H); // P*(T'*H)
    else
        W = X*H;  
}

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
    void Estep(const MatrixBase<derived>  &X, const std::optional<Matrix<T,Dynamic,Dynamic>> loadings,
               const Matrix<T,Dynamic,Dynamic> &W,
               Matrix<T,Dynamic,Dynamic> &H, std::optional<Vector<T,Dynamic>> diagCV = std::nullopt)
{
    Matrix<T,Dynamic,Dynamic> Wn(W.rows(),W.cols());
    if (diagCV.has_value())
        // root-mean scale the spectral components
        Wn.array() = W.array().colwise() / diagCV.value().array().sqrt();
    else
        Wn = W;
    normalize(Wn,2);
    if (diagCV.has_value())
        // implicitly rootmean scale the data
        Wn.array() = Wn.array().colwise() / diagCV.value().array().sqrt();

    if (loadings.has_value())
        H = X*(loadings.value().transpose()*Wn);   // T*(P'*wn)
    else
        H = (Wn.transpose()*X).transpose();
    Index max_index;
    T max_val;
    for (auto i = 0; i < H.rows(); i++) {
        max_val = H.row(i).maxCoeff(&max_index);
        H.row(i).setZero();
        H.row(i)(max_index) = max_val;
    }
    normalize(H,2);
}

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
    void Mstep(const MatrixBase<derived>  &X, const std::optional<Matrix<T,Dynamic,Dynamic>> loadings,
               const Matrix<T,Dynamic,Dynamic> &H, Matrix<T,Dynamic,Dynamic> &W)
{
    if (loadings.has_value())
        W = loadings.value()*(X.transpose()*H); // P*(T'*H)
    else
        W = X*H;  
}

// Frobius norm of (possibly scaled) residuals X - WH' assuming that H is orthonormal
template <typename T>
    T frobeniusError(const Matrix<T,Dynamic,Dynamic>  &W, const T sumXsqr,
               std::optional<Vector<T,Dynamic>> diagCV = std::nullopt)
{
    Matrix<T,Dynamic,Dynamic> Wn(W.rows(),W.cols());
    if (diagCV.has_value())
        Wn.array() = W.array().colwise() / diagCV.value().array().sqrt();
    else
        Wn = W;
    return std::sqrt((sumXsqr - Wn.array().square().sum()));  
}

// When analyzing the full dataset, assume X is "wide"
template <typename derived, typename derivedI,
typename T = typename MatrixBase<derived>::Scalar >
    T trainModel(const MatrixBase<derived> &X, const std::optional<Matrix<T,Dynamic,Dynamic>> loadings,
                 const int nClust, const int nReplicates,
                 MatrixBase<derived> &Centroid, MatrixBase<derived> &H,
                 MatrixBase<derivedI> &clustID, bool doWeighting=true)
{
    constexpr int maxiter = 100;
    T tol;
    if constexpr(std::is_same<T, float>::value)
        tol = 1e-5;
    else
        tol = 1e-6;
    auto nFeatures = X.rows();
    auto nSamples = X.cols();

    // Get the covariance vector = mean spectrum, if desired
    std::optional<Vector<T,Dynamic>> diagCV;
    if (!loadings.has_value())
        diagCV = getDiagCV(X,doWeighting);
    else {
        if (doWeighting)
            diagCV = loadings.value() * (X.colwise().mean()).transpose();
        else
            diagCV = std::nullopt;
    }

    // Get total sum of the squares of (possibly scaled) data
    T sumXsqr;
    if (loadings.has_value()) {
        Matrix<T,Dynamic,Dynamic> cxpT = X.transpose()*X;
        Matrix<T,Dynamic,Dynamic> cxpP;
        if (diagCV.has_value()) {
            Matrix<T,Dynamic,Dynamic> wtP = loadings.value();
            wtP.array().colwise() /= diagCV.value().array();
            cxpP = loadings.value().transpose() * wtP;
        }
        else {
            cxpP = loadings.value().transpose()*loadings.value();
        }
       sumXsqr =(cxpT*cxpP).trace();
    }
    else {
        if (diagCV)
            sumXsqr = (X.rowwise().squaredNorm().array()/diagCV.value().array() ).sum();
        else
            sumXsqr = X.rowwise().squaredNorm().sum();
    }

    Centroid.setZero();
    clustID.setZero();

    Matrix<T,Dynamic,Dynamic> thisW(nFeatures, nClust);
    Matrix<T,Dynamic,Dynamic> thisH(nSamples, nClust);

    T thiscost;
    T oldcost = std::numeric_limits<T>::infinity();
    T cost = oldcost;
    for (auto rep=0; rep < nReplicates; rep++) {
        initializeEM(X,loadings,thisH,thisW);
        // oldcost = frobeniusError(thisW, sumXsqr, diagCV);
        for (auto i = 0; i < maxiter; i++) {
            Estep(X,loadings,thisW,thisH,diagCV);
            Mstep(X,loadings,thisH,thisW);
            thiscost = frobeniusError(thisW, sumXsqr, diagCV);
            // printf("%g\n",thiscost);
            if (std::abs(oldcost-thiscost) <= tol*std::abs(thiscost))
                break;
            else
                oldcost = thiscost;
        }
        if (thiscost < cost) {
            cost = thiscost;
            // nnz = the number of non-zeros in each column of thisH;
            Vector<T,Dynamic> nnz = (thisH.matrix().array() > 0).colwise().count().template cast<T>();
            PermutationMatrix P = sortPermMat(nnz); // order by fraction
            Centroid = thisW*P;
            H = thisH*P;
        }
    }

    // Make the cluster assignments
    rowmax2index(H,clustID);
    renormalize(Centroid,H,1); // spectral component as ion fraction, H as counts
    return cost;
}

// Overloaded function to analyze full dataset
template <typename derived, typename derivedI,
typename T = typename MatrixBase<derived>::Scalar >
    T trainModel(const MatrixBase<derived> &X,
                 const int nClust, const int nReplicates,
                 MatrixBase<derived> &Centroid, MatrixBase<derived> &H,
                 MatrixBase<derivedI> &clustID, bool doWeighting=true)
{
    std::optional<Matrix<T,Dynamic,Dynamic>> P = std::nullopt;
    T cost = trainModel(X, P, nClust, nReplicates, Centroid, H, clustID, doWeighting);
    return cost;
}

// Overloaded function for analyzing PCA representation of data
// This assumes the scores and loading matrices are "tall"
template <typename derivedR, typename derivedI,
typename T = typename MatrixBase<derivedR>::Scalar >
    T trainModel(const MatrixBase<derivedR> &scores, const MatrixBase<derivedR> &loadings,
                 const int nClust, const int nReplicates,
                 MatrixBase<derivedR> &Centroid, MatrixBase<derivedR> &H,
                 MatrixBase<derivedI> &clustID, bool doWeighting=false)
{
    std::optional<Matrix<T,Dynamic,Dynamic>> P = loadings;
    T cost = trainModel(scores, P, nClust, nReplicates, Centroid, H, clustID, doWeighting);
    return cost;
}

#endif /* __ONMF_H__ */