import { Policy, Coverage } from '../types/policy';
import apiClient from './apiClient';

export const getPolicies = async (customerId = 'cust-001'): Promise<Policy[]> => {
  const res = await apiClient.get('/policies', { params: { customerId } });
  return res.data.policies;
};

export const getPolicy = async (id: string): Promise<Policy> => {
  const res = await apiClient.get(`/policies/${id}`);
  return res.data;
};

export const updateCoverage = async (
  policyId: string,
  covId: string,
  coverage: Partial<Coverage>
): Promise<Coverage> => {
  const res = await apiClient.put(`/policies/${policyId}/coverages/${covId}`, coverage);
  return res.data.coverage;
};
