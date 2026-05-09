export interface Coverage {
  id: string;
  policyId: string;
  type: string;
  limit: number;
  deductible: number;
  description: string;
}

export interface Policy {
  id: string;
  customerId: string;
  policyNumber: string;
  type: string;
  status: string;
  premium: number;
  startDate: string;
  endDate: string;
  coverages: Coverage[];
}
