import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { PaymentTransactionDto } from '../types/portal';

export const paymentApi = createApi({
  reducerPath: 'paymentApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  tagTypes: ['Payments'],
  endpoints: (builder) => ({
    getPaymentHistory: builder.query<PaymentTransactionDto[], string>({
      query: (policyId) => `/payments/${policyId}/history`,
      providesTags: ['Payments'],
    }),
    setBillingSchedule: builder.mutation<void, { policyId: string; frequency: string }>({
      query: ({ policyId, frequency }) => ({ url: `/payments/${policyId}/schedule`, method: 'POST', body: { frequency } }),
      invalidatesTags: ['Payments'],
    }),
  }),
});

export const { useGetPaymentHistoryQuery, useSetBillingScheduleMutation } = paymentApi;
