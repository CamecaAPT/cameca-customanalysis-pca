#ifndef __BLAS_WRAPPER_H__
#define __BLAS_WRAPPER_H__

#include "mkl.h"

// A, B and C are matrices
// S is a symmetric matrix (only Upper used)
// x, y are vectors
// a, b and c are real scalars
// m, n, k are integer scalars
// t indicates transpose, p indicates plus, eq indicates equals


// ********************* Level 1 BLAS *************************************

// x'y   dot product
float blas_dot(const MKL_INT m, const float *x, const float *y)
{
    return cblas_sdot(m,x,1,y,1);
}

double blas_dot(const MKL_INT m, const double *x, const double *y)
{
    return cblas_ddot(m,x,1,y,1);
}

// ********************* Level 2 BLAS *************************************
// Matrix A is m x n

// A = axy' + A  outer product update
void blas_product_axytpA(const MKL_INT m, const MKL_INT n, const double a,
                         const double *x, const double *y, double *A)
{
    cblas_dger(CblasColMajor,m,n,a,x,1,y,1,A,m);
}

void blas_product_axytpA(const MKL_INT m, const MKL_INT n, const float a,
                         const float *x, const float *y, float *A)
{
    cblas_sger(CblasColMajor,m,n,a,x,1,y,1,A,m);
}

// A = axx' + A  symmetric outer product update
void blas_product_axxtpA(const MKL_INT m, const float a,  const float *x, float *A)
{
    cblas_ssyr(CblasColMajor,CblasUpper,m,a,x,1,A,m);
}

void blas_product_axxtpA(const MKL_INT m, const double a,  const double *x, double *A)
{
    cblas_dsyr(CblasColMajor,CblasUpper,m,a,x,1,A,m);
}

// y = Ax  matrix-vector product
void blas_product_Ax(const MKL_INT m, const MKL_INT n, const float *A, const float *x, float *y)
{
        cblas_sgemv(CblasColMajor,CblasNoTrans,m,n,1.0,A,m,x,1,0.0,y,1);
}

void blas_product_Ax(const MKL_INT m, const MKL_INT n, const double *A, const double *x, double *y)
{
    cblas_dgemv(CblasColMajor,CblasNoTrans,m,n,1.0,A,m,x,1,0.0,y,1);
}

// y = A'x; transposed matrix-vector product
void blas_product_Atx(const MKL_INT m, const MKL_INT n, const float *A, const float *x, float *y)
{
    cblas_sgemv(CblasColMajor,CblasTrans,m,n,1.0,A,m,x,1,0.0,y,1);
}

void blas_product_Atx(const MKL_INT m, const MKL_INT n, const double *A, const double *x, double *y)
{
    cblas_dgemv(CblasColMajor,CblasTrans,m,n,1.0,A,m,x,1,0.0,y,1);
}

// y = aAx + by
void blas_product_aAxpby(const MKL_INT m, const MKL_INT n, const float a,
                         const float *A, const float *x, const float b, float *y)
{
        cblas_sgemv(CblasColMajor,CblasNoTrans,m,n,a,A,m,x,1,b,y,1);
}

void blas_product_aAxpby(const MKL_INT m, const MKL_INT n, const double a,
                         const double *A, const double *x, const double b, double *y)
{
    cblas_dgemv(CblasColMajor,CblasNoTrans,m,n,a,A,m,x,1,b,y,1);
}

// y = aA'x + by;
void blas_product_aAtxpby(const MKL_INT m, const MKL_INT n, const float a, const float *A,
                          const float *x, const float b, float *y)
{
        cblas_sgemv(CblasColMajor,CblasTrans,m,n,a,A,m,x,1,b,y,1);
}

void blas_product_aAtxpby(const MKL_INT m, const MKL_INT n, const double a, const double *A,
                          const double *x, const double b, double *y)
{
    cblas_dgemv(CblasColMajor,CblasTrans,m,n,a,A,m,x,1,b,y,1);
}

// x = Tx symmetric matrix-vector product
//        square upper triangular T, x is overwritten
void blas_product_Ux(const MKL_INT m, const float *U,  float *x)
{
    cblas_strmv(CblasColMajor,CblasUpper,CblasNoTrans,CblasNonUnit,
                m, U, m, x, 1);
}   

void blas_product_Ux(const MKL_INT m, const double *U,  double *x)
{
    cblas_dtrmv(CblasColMajor,CblasUpper,CblasNoTrans,CblasNonUnit,
                    m, U, m, x, 1);
}   

// x = T'x for square upper triangular T, x is overwritten
void blas_product_Utx(const MKL_INT m, const float *U,  float *x)
{
    cblas_strmv(CblasColMajor,CblasUpper,CblasTrans,CblasNonUnit,
                m, U, m, x, 1);
}   

void blas_product_Utx(const MKL_INT m, const double *U,  double *x)
{
    cblas_dtrmv(CblasColMajor,CblasUpper,CblasTrans,CblasNonUnit,
                    m, U, m, x, 1);
}   

// ********************** General Matrix-Matrix Products ******************
// Matrix C is m x n, k is the inner dimension of op(A) and op(B),
// lda and ldb relate to A and B in the calling program

// C = A*B  general matrix product
void blas_product_AB(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                     const float *A, const float *B, float *C)
{
    cblas_sgemm(CblasColMajor,CblasNoTrans,CblasNoTrans,m,n,k,1.0,A,m,B,k,0.0,C,m);
}

void blas_product_AB(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                     const double *A, const double *B, double *C)
{
        cblas_dgemm(CblasColMajor,CblasNoTrans,CblasNoTrans,m,n,k,1.0,A,m,B,k,0.0,C,m);
}

// C = A'*B   general matrix product
void blas_product_AtB(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                      const float *A, const float *B, float *C)
{
    cblas_sgemm(CblasColMajor,CblasTrans,CblasNoTrans,m,n,k,1.0,A,k,B,k,0.0,C,m);
}

void blas_product_AtB(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                      const double *A, const double *B, double *C)
{
    cblas_dgemm(CblasColMajor,CblasTrans,CblasNoTrans,m,n,k,1.0,A,k,B,k,0.0,C,m);
}

// C = A*B'   general matrix product
void blas_product_ABt(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                      const float *A, const float *B, float *C)
{
    cblas_sgemm(CblasColMajor,CblasNoTrans,CblasTrans,m,n,k,1.0,A,m,B,n,0.0,C,m);
}

void blas_product_ABt(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                      const double *A, const double *B, double *C)
{
    cblas_dgemm(CblasColMajor,CblasNoTrans,CblasTrans,m,n,k,1.0,A,m,B,n,0.0,C,m);
}

// C = A'*B'   general matrix product
void blas_product_AtBt(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                       const float *A, const float *B, float *C)
{
    cblas_sgemm(CblasColMajor,CblasTrans,CblasTrans,m,n,k,1.0,A,k,B,n,0.0,C,m);
}

void blas_product_AtBt(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                       const double *A, const double *B, double *C)
{
    cblas_dgemm(CblasColMajor,CblasTrans,CblasTrans,m,n,k,1.0,A,k,B,n,0.0,C,m);
}

// C = aA*B + cC
void blas_product_aABpcC(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                 const float a, const float *A, const float *B, const float c, float *C)
{
    cblas_sgemm(CblasColMajor,CblasNoTrans,CblasNoTrans,m,n,k,a,A,m,B,k,c,C,m);
}

void blas_product_aABpcC(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                 const double a, const double *A, const double *B, const double c, double *C)
{
    cblas_dgemm(CblasColMajor,CblasNoTrans,CblasNoTrans,m,n,k,a,A,m,B,k,c,C,m);
}

// C = aA*B' + cC
void blas_product_aABtpcC(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                 const float a, const float *A, const float *B, const float c, float *C)
{
    cblas_sgemm(CblasColMajor,CblasNoTrans,CblasTrans,m,n,k,a,A,m,B,n,c,C,m);
}

void blas_product_aABtpcC(const MKL_INT m, const MKL_INT n, const MKL_INT k,
                 const double a, const double *A, const double *B, const double c, double *C)
{
    cblas_dgemm(CblasColMajor,CblasNoTrans,CblasTrans,m,n,k,a,A,m,B,n,c,C,m);
}

// ********************** Symmetric Matrix-Matrix Products ****************

// S = A'*A
void blas_product_AtA(const MKL_INT nA, const MKL_INT mA, const float *A, float *S)
{
    cblas_ssyrk(CblasColMajor,CblasUpper,CblasTrans,nA,mA,
                1.0,A,mA,0.0,S,nA);
}

void blas_product_AtA(const MKL_INT nA, const MKL_INT mA, const double *A, double *S)
{
        cblas_dsyrk(CblasColMajor,CblasUpper,CblasTrans,nA,mA,
                    1.0,A,mA,0.0,S,nA);
}

// S = aA'*A + cS
void blas_product_aAtApcC(const MKL_INT nA, const MKL_INT mA,
                 const float a, const float *A, const float c,float *S)
{
    cblas_ssyrk(CblasColMajor,CblasUpper,CblasTrans,nA,mA,a,A,mA,c,S,nA);
}

void blas_product_aAtApcC(const MKL_INT nA, const MKL_INT mA,
                 const double a, const double *A, const double c,double *S)
{
    cblas_dsyrk(CblasColMajor,CblasUpper,CblasTrans,nA,mA,a,A,mA,c,S,nA);
}

// S = A*A'
void blas_product_AAt(const MKL_INT mA, const MKL_INT nA, const float *A, float *S)
{
    cblas_ssyrk(CblasColMajor,CblasUpper,CblasNoTrans,mA,nA,
                1.0,A,mA,0.0,S,mA);
}

void blas_product_AAt(const MKL_INT mA, const MKL_INT nA, const double *A, double *S)
{
        cblas_dsyrk(CblasColMajor,CblasUpper,CblasNoTrans,mA,nA,
                    1.0,A,mA,0.0,S,mA);
}

// S = aA*A' + cS
void blas_product_aAAtpcC(const MKL_INT mA, const MKL_INT nA,
                 const float a, const float *A, const float c,float *S)
{
    cblas_ssyrk(CblasColMajor,CblasUpper,CblasNoTrans,mA,nA,
                a,A,mA,c,S,mA);
}

void blas_product_aAAtpcC(const MKL_INT mA, const MKL_INT nA,
                 const double a, const double *A, const double c,double *S)
{
        cblas_dsyrk(CblasColMajor,CblasUpper,CblasNoTrans,mA,nA,
                    a,A,mA,c,S,mA);
}

// ********************** Symmetric-General Matrix Products ***************

// C = S*B  for symmetric S
void blas_product_SB(const MKL_INT mC, const MKL_INT nC,
                     const float *S, const float *B, float *C)
{
    cblas_ssymm(CblasColMajor,CblasLeft,CblasUpper,mC,nC,
                1.0,S,mC,B,mC,0.0,C,mC);
}

void blas_product_SB(const MKL_INT mC, const MKL_INT nC,
                     const double *S, const double *B, double *C)
{
        cblas_dsymm(CblasColMajor,CblasLeft,CblasUpper,mC,nC,
                    1.0,S,mC,B,mC,0.0,C,mC);
}

// C = B*S  for symmetric S
void blas_product_BS(const MKL_INT mC, const MKL_INT nC,
                     const float *S, const float *B, float *C)
{
    cblas_ssymm(CblasColMajor,CblasRight,CblasUpper,mC,nC,
                1.0,S,nC,B,mC,0.0,C,mC);
}

void blas_product_BS(const MKL_INT mC, const MKL_INT nC,
                     const double *S, const double *B, double *C)
{
        cblas_dsymm(CblasColMajor,CblasRight,CblasUpper,mC,nC,
                    1.0,S,nC,B,mC,0.0,C,mC);
}

// ********************* Triangular matrix routines **********************

// Solve U*A = B  for square upper triangular U, A is overwritten with B
void blas_solve_TAeqB(const MKL_INT mB, const MKL_INT nB, const float *U, float *AB)
{
    cblas_strsm(CblasColMajor,CblasLeft,CblasUpper,CblasNoTrans,CblasNonUnit,
                mB, nB,1.0,U,mB,AB,mB);
}

void blas_solve_TAeqB(const MKL_INT mB, const MKL_INT nB, const double *U, double *AB)
{
        cblas_dtrsm(CblasColMajor,CblasLeft,CblasUpper,CblasNoTrans,CblasNonUnit,
                    mB, nB,1.0,U,mB,AB,mB);
}

#endif /* __BLAS_WRAPPER_H__ */