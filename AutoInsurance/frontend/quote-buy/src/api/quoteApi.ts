import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { CoverageType, Driver, PersonalInfo, PolicyBound, ReviewData, SelectedCoverage, Vehicle } from '../types/quote';

interface CreateQuoteResponse { quoteId: string; quoteNumber: string; sessionToken: string; stepReached: number; }
interface ResumeQuoteResponse { quoteId: string; quoteNumber: string; draftJson: string; stepReached: number; }

export const quoteApi = createApi({
  reducerPath: 'quoteApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  endpoints: (builder) => ({
    createQuote: builder.mutation<CreateQuoteResponse, PersonalInfo>({
      query: (body) => ({ url: '/quote', method: 'POST', body }),
    }),
    saveDrivers: builder.mutation<void, { quoteId: string; drivers: Driver[] }>({
      query: ({ quoteId, drivers }) => ({
        url: `/quote/${quoteId}/drivers`,
        method: 'PATCH',
        body: drivers.map(d => ({
          driverType: d.isPrimary ? 'Primary' : 'Additional',
          firstName: d.firstName,
          lastName: d.lastName,
          dateOfBirth: d.dateOfBirth,
          licenseNumber: d.licenseNumber,
          licenseState: d.licenseState,
        })),
      }),
    }),
    saveVehicles: builder.mutation<void, { quoteId: string; vehicles: Vehicle[] }>({
      query: ({ quoteId, vehicles }) => ({ url: `/quote/${quoteId}/vehicles`, method: 'PATCH', body: vehicles }),
    }),
    saveCoverages: builder.mutation<void, { quoteId: string; coverages: SelectedCoverage[] }>({
      query: ({ quoteId, coverages }) => ({
        url: `/quote/${quoteId}/coverages`,
        method: 'PATCH',
        body: coverages.map(c => ({
          coverageTypeId: c.coverageTypeId,
          limitOption: c.limits,
          deductible: 0,
        })),
      }),
    }),
    getReview: builder.query<ReviewData, string>({
      query: (quoteId) => `/quote/${quoteId}/review`,
    }),
    bindQuote: builder.mutation<PolicyBound, string>({
      query: (quoteId) => ({ url: `/quote/${quoteId}/bind`, method: 'POST' }),
    }),
    resumeQuote: builder.mutation<ResumeQuoteResponse, { quoteNumber: string; zipCode: string }>({
      query: (body) => ({ url: '/quote/resume', method: 'POST', body }),
    }),
    getCoverageTypes: builder.query<CoverageType[], void>({
      query: () => '/quote/coverage-types',
    }),
    saveDraft: builder.mutation<void, { quoteId: string; draftJson: string; stepReached: number }>({
      query: ({ quoteId, ...body }) => ({ url: `/quote/${quoteId}/draft`, method: 'PATCH', body }),
    }),
  }),
});

export const {
  useCreateQuoteMutation,
  useSaveDriversMutation,
  useSaveVehiclesMutation,
  useSaveCoveragesMutation,
  useGetReviewQuery,
  useBindQuoteMutation,
  useResumeQuoteMutation,
  useGetCoverageTypesQuery,
  useSaveDraftMutation,
} = quoteApi;
