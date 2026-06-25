import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

interface InitiatePaymentResponse { transactionId: string; paymentIntentId: string; amount: number; }
interface ConfirmPaymentResponse { transactionRef: string; success: boolean; policyId: string; }

export const paymentApi = createApi({
  reducerPath: 'paymentApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  endpoints: (builder) => ({
    initiatePayment: builder.mutation<InitiatePaymentResponse, { policyId: string; amount: number }>({
      query: (body) => ({ url: '/payments/initiate', method: 'POST', body }),
    }),
    confirmPayment: builder.mutation<ConfirmPaymentResponse, { policyId: string; paymentIntentId: string }>({
      query: (body) => ({ url: '/payments/confirm', method: 'POST', body }),
    }),
  }),
});

export const { useInitiatePaymentMutation, useConfirmPaymentMutation } = paymentApi;
