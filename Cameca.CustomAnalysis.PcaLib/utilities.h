#ifndef __UTILITIES_H__
#define __UTILITIES_H__


#include <cstdio>
#include <string>
#include <random>
#include <limits>
#include <chrono>
#include <numeric>
#include <optional>
#include <algorithm>
#include <type_traits>
#include <Eigen/Dense>

using namespace Eigen;

// timing routines
// usage: auto start = tic();  and double duration = toc(start);
inline std::chrono::steady_clock::time_point tic()
{
    return std::chrono::steady_clock::now();
}

inline double toc(std::chrono::steady_clock::time_point start)
{
    std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::chrono::duration<double> time = end - start;
    return time.count();
}


// Initialize a global random number generator
inline std::mt19937& getGlobalRNG() {
    static std::mt19937 rng(std::random_device{}()); // {} constructs a temporary object
    return rng;
}

// return a single uniform random number
template <typename T>
inline T rand(const T minVal, const T maxVal)
{
    if constexpr (std::is_floating_point_v<T>) {
        std::uniform_real_distribution<T> distr(minVal,maxVal);
        return distr(getGlobalRNG());
    }
    else {
        std::uniform_int_distribution<T> distr(minVal,maxVal);
        return distr(getGlobalRNG());
    }
}

// fill a matrix with random numbers from the supplied distribution
// e.g. std::uniform_real_distribution<double> dist(10,100);
template <typename derived, typename Dist>
inline void fillRandom(Eigen::MatrixBase<derived> &aMatrix, Dist& dist) {
    std::generate(aMatrix.reshaped().array().begin(),
                  aMatrix.reshaped().array().end(),
                  [&](){return dist(getGlobalRNG());});
}

// Columnwise normalize a matrix (1-, 2- or infinity norm)
template <typename derived, typename T = typename Eigen::MatrixBase<derived>::Scalar>
inline void normalize(Eigen::MatrixBase<derived> &mat, int norm) {
    Eigen::RowVector<T, Eigen::Dynamic> colnorm(mat.cols());
    if (norm ==1)
        colnorm = mat.colwise().template lpNorm<1>();
    else if (norm ==2)
        colnorm = mat.colwise().template lpNorm<2>();
    else if (norm == Eigen::Infinity)
        colnorm = mat.colwise().template lpNorm<Infinity>();
    else
        return;

    mat.array().rowwise() /= colnorm.array();
}

template <typename derived, typename T = typename Eigen::MatrixBase<derived>::Scalar>
inline void normalize(Eigen::MatrixBase<derived> &mat, int norm,
               Eigen::RowVector<T,Eigen::Dynamic> &colnorm) {
    if (norm ==1)
        colnorm = mat.colwise().template lpNorm<1>();
    else if (norm ==2)
        colnorm = mat.colwise().template lpNorm<2>();
    else if (norm == Eigen::Infinity)
        colnorm = mat.colwise().template lpNorm<Infinity>();
    else
        return;

    mat.array().rowwise() /= colnorm.array();
}

// Bound the elements of a matrix
template <typename derived, typename T = typename Eigen::MatrixBase<derived>::Scalar>
inline void boundMatrix(Eigen::MatrixBase<derived> &aMatrix, T minval, T maxval) {
    std::transform(aMatrix.reshaped().array().begin(),
                  aMatrix.reshaped().array().end(),
                  aMatrix.reshaped().array().begin(),
                  [minval,maxval](T val){return std::clamp(val,minval,maxval);});
}

template <typename T>
inline bool isSortedDescending(Eigen::Matrix<T, Eigen::Dynamic, 1> &v) {
    return std::is_sorted(v.begin(),v.end(),std::greater<T>());
}

// construct a full symmetric matrix from an upper triangle
template <typename T>
inline void copyUtoL(Eigen::MatrixBase<T> &A) {
    A = A.template selfadjointView<Eigen::Upper>();
}

// construct a full symmetric matrix from an upper triangle
template <typename T>
inline void copyLtoU(Eigen::MatrixBase<T> &A) {
    A = A.template selfadjointView<Eigen::Lower>();
}

// Construct the optional noise variance vector = mean spectrum
// Note diagCV.value() has type Eigen::VectorT<T>
template <typename derived, typename T = typename Eigen::MatrixBase<derived>::Scalar>
inline std::optional<Eigen::Matrix<T, Eigen::Dynamic,1>>
    getDiagCV(const Eigen::MatrixBase<derived> &X, bool doWeighting = true)
{
    std::optional<Eigen::Matrix<T, Eigen::Dynamic,1>> diagCV;
    int nFeatures = X.rows();
    if (doWeighting) {
        diagCV = X.rowwise().mean();
        // if any elements d of diagCV <= 0, sqrt(d) and 1/d will cause problems
        if (!(diagCV.value().array()>0).all()) {
            diagCV.value().setOnes(nFeatures);
            printf("Negative variances, reverting to unweighted algorithm\n");
        }
    }
    else
        diagCV = std::nullopt;

    return diagCV;
}

// Get vecctor of indices of row-wise maxima
template <typename derived, typename derived2>
inline void rowmax2index(const Eigen::MatrixBase<derived> &mat, Eigen::MatrixBase<derived2> &indx)
{
    using T = typename Eigen::MatrixBase<derived2>::Scalar;
    Index maxIndex;
    Index nSamples = mat.rows();
    for (auto i = 0; i < nSamples; i++) {
        mat.row(i).array().maxCoeff(&maxIndex);
        indx(i) = static_cast<T>(maxIndex);
    }
}

// Get vecctor of indices of column-wise maxima
template <typename derived, typename derived2>
inline void colmax2index(const Eigen::MatrixBase<derived> &mat, Eigen::MatrixBase<derived2> &indx)
{
    using T = typename Eigen::MatrixBase<derived2>::Scalar;
    Index maxIndex;
    Index nSamples = mat.cols();
    for (auto i = 0; i < nSamples; i++) {
        mat.col(i).array().maxCoeff(&maxIndex);
        indx(i) = static_cast<T>(maxIndex);
    }
}

template <typename T>
inline void print_matrix(const Eigen::MatrixBase<T> &m) {
    std::string fmtStr;
    if (std::is_floating_point<typename T::Scalar>())
        fmtStr = "%9.4g ";
    else
        fmtStr = "%d ";
    int r = m.rows();
    int c = m.cols();
    for (int i=0; i<r; i++) {
        for (int j=0; j<c; j++) {
            printf(fmtStr.c_str(),m(i,j));
        }
        printf("\n");
    }
    printf("\n");
}

template <typename T>
inline void print_matrix(const Eigen::MatrixBase<T> &m, const std::string fmtStr) {
    int r = m.rows();
    int c = m.cols();
    for (int i=0; i<r; i++) {
        for (int j=0; j<c; j++) {
            printf(fmtStr.c_str(),m(i,j));
        }
        printf("\n");
    }
    printf("\n");
}

// Resolve sign ambiguity by choosing the columns of B predominantly positive
// There is probably a simpler way to do this
template <typename T, typename derived>
inline void makePos(Eigen::MatrixBase<derived> &A, Eigen::MatrixBase<derived> &B)
{
    Eigen::Array<T,Eigen::Dynamic,1> posX = B.cwiseMax(0).array().square().colwise().sum();
    Eigen::Array<T,Eigen::Dynamic,1> allX = B.array().square().colwise().sum();
    Eigen::Array<T,1,Eigen::Dynamic> signs = posX / allX - 0.5;
    signs = signs / signs.abs();

    A.array().rowwise() *= signs;
    B.array().rowwise() *= signs;
}    

// Columnwise normalize the first argument, apply inverse to second 
template <typename T>
inline void renormalize(Eigen::MatrixBase<T> &S, Eigen::MatrixBase<T> &A, int norm) {
    Eigen::RowVector<typename T::Scalar, Eigen::Dynamic> colnorm(S.cols());
    if (norm ==1)
        colnorm = S.colwise().template lpNorm<1>();
    else if (norm ==2)
        colnorm = S.colwise().template lpNorm<2>();
    else if (norm == Eigen::Infinity)
        colnorm = S.colwise().template lpNorm<Infinity>();
    
    S = S.array().rowwise() / colnorm.array();
    A = A.array().rowwise() * colnorm.array();
}

template <typename T>
inline void varianceReorder(Eigen::Matrix<T,Eigen::Dynamic,Eigen::Dynamic> &A,
                     Eigen::Matrix<T,Eigen::Dynamic,Eigen::Dynamic> &S) {
    int nComp = A.cols();

    Eigen::Vector<T,Eigen::Dynamic> var = (A.transpose() * A) * (S.transpose() * S).diagonal();
    Eigen::VectorXi varIndex(nComp);
    std::iota(varIndex.begin(),varIndex.end(),0);
    std::stable_sort(varIndex.begin(), varIndex.end(),
                     [&var](int i1, int i2) {return var[i1]>var[i2];});
    Eigen::PermutationMatrix<Eigen::Dynamic,Eigen::Dynamic> perm(varIndex);
    A *= perm;
    S *= perm;
}

// Post-multiply to sort columns, pre-multiply by transpose to sort rows
template <typename T>
inline Eigen::PermutationMatrix<Eigen::Dynamic,Eigen::Dynamic> sortPermMat(const Eigen::Vector<T,Eigen::Dynamic> &permVec)
{
    int nComp = permVec.size();
    Eigen::VectorXi varIndex(nComp);
    std::iota(varIndex.begin(),varIndex.end(),0);
    std::stable_sort(varIndex.begin(), varIndex.end(),
                     [&permVec](int i1, int i2) {return permVec[i1]<permVec[i2];});
    Eigen::PermutationMatrix<Eigen::Dynamic,Eigen::Dynamic> perm(varIndex);
    return perm;
}


// Accurately computing the log-sum-exp and softmax functions
// P. Blanchard, D. J. Higham and N. J. Higham
// IMA Journal of Numerical Analysis 2021 Vol. 41 Issue 4 Pages 2311-2330
// DOI: 10.1093/imanum/draa038
// I/O: llkObs is nSample x nComp Log-likelihood matrix
//      f is the nSample x 1 vector of row-wise log-likelihoods
//      g is the nSample x nComp gradient = posterior probabilities
template <typename derived, typename T=typename derived::Scalar>
inline T logsumexp(const Eigen::MatrixBase<derived> &llkObs, Eigen::MatrixBase<derived> &g)
{
    int nSamples = llkObs.rows();
    Eigen::Vector<T,Eigen::Dynamic> f = llkObs.rowwise().maxCoeff();
    Eigen::Matrix<T,Eigen::Dynamic,Eigen::Dynamic> w  = (llkObs.colwise() - f).array().exp();
    Eigen::Vector<T,Eigen::Dynamic> s = w.rowwise().sum();
    T wMax;
    int wMaxIndex;
    for (auto r = 0; r < nSamples; r++) {
        wMax = w.row(r).array().maxCoeff(&wMaxIndex);
        s(r) -= wMax;
    }
    f.array() += s.array().log1p();
    g.array() = w.array().colwise() / (1+s.array());
    return -f.sum(); // Negative log-likelihood of the data given the model
}

#endif /* __UTILITIES_H__ */