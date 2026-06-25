import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { AccountDto, CoverageChangeDto, DocumentDto, PolicyDetail, PolicySummary } from '../types/portal';

export const customerApi = createApi({
  reducerPath: 'customerApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  tagTypes: ['Policy', 'Documents', 'Account'],
  endpoints: (builder) => ({
    getPolicies: builder.query<PolicySummary[], void>({
      query: () => '/policies',
      providesTags: ['Policy'],
    }),
    getPolicyDetail: builder.query<PolicyDetail, string>({
      query: (id) => `/policies/${id}`,
      providesTags: ['Policy'],
    }),
    getDocuments: builder.query<DocumentDto[], string>({
      query: (policyId) => `/documents/${policyId}`,
      providesTags: ['Documents'],
    }),
    generateDocument: builder.mutation<DocumentDto, { policyId: string; documentType: string }>({
      query: (body) => ({ url: '/documents/generate', method: 'POST', body }),
      invalidatesTags: ['Documents'],
    }),
    changeCoverage: builder.mutation<string, { policyId: string; changes: CoverageChangeDto[] }>({
      query: ({ policyId, changes }) => ({ url: `/policies/${policyId}/coverages`, method: 'PUT', body: { changes } }),
      invalidatesTags: ['Policy'],
    }),
    renewPolicy: builder.mutation<void, string>({
      query: (policyId) => ({ url: `/policies/${policyId}/renew`, method: 'POST' }),
      invalidatesTags: ['Policy'],
    }),
    getAccount: builder.query<AccountDto, void>({
      query: () => '/account',
      providesTags: ['Account'],
    }),
    linkAccount: builder.mutation<string, { policyId: string; email: string }>({
      query: (body) => ({ url: '/account/link', method: 'POST', body }),
      invalidatesTags: ['Account'],
    }),
  }),
});

export const {
  useGetPoliciesQuery,
  useGetPolicyDetailQuery,
  useGetDocumentsQuery,
  useGenerateDocumentMutation,
  useChangeCoverageMutation,
  useRenewPolicyMutation,
  useGetAccountQuery,
  useLinkAccountMutation,
} = customerApi;
