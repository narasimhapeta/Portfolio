import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { ClaimDetail, ClaimDto } from '../types/portal';

export const claimsApi = createApi({
  reducerPath: 'claimsApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  tagTypes: ['Claims'],
  endpoints: (builder) => ({
    getClaims: builder.query<ClaimDto[], string>({
      query: (policyId) => `/claims?policyId=${policyId}`,
      providesTags: ['Claims'],
    }),
    getClaimDetail: builder.query<ClaimDetail, string>({
      query: (claimId) => `/claims/${claimId}`,
      providesTags: ['Claims'],
    }),
    submitClaim: builder.mutation<{ claimId: string }, { policyId: string; incidentDate: string; description: string }>({
      query: (body) => ({ url: '/claims', method: 'POST', body }),
      invalidatesTags: ['Claims'],
    }),
    uploadClaimDocument: builder.mutation<{ documentId: string }, { claimId: string; documentType: string; base64Content: string; fileName: string }>({
      query: ({ claimId, ...body }) => ({ url: `/claims/${claimId}/documents`, method: 'POST', body }),
      invalidatesTags: ['Claims'],
    }),
  }),
});

export const { useGetClaimsQuery, useGetClaimDetailQuery, useSubmitClaimMutation, useUploadClaimDocumentMutation } = claimsApi;
