export interface PolicySummary {
  id: string;
  policyNumber: string;
  status: string;
  effectiveDate: string;
  expirationDate: string;
  totalAnnualPremium: number;
}

export interface PolicyDriver { firstName: string; lastName: string; licenseNumber: string; licenseState: string; isPrimary: boolean; }
export interface PolicyVehicle { year: number; make: string; model: string; vin: string; primaryUse: string; }
export interface PolicyCoverage { id: number; name: string; limits: string; annualPremium: number; }
export interface EndorsementDto { id: string; type: string; description: string; effectiveDate: string; createdAt: string; }

export interface PolicyDetail {
  id: string;
  policyNumber: string;
  status: string;
  effectiveDate: string;
  expirationDate: string;
  totalAnnualPremium: number;
  drivers: PolicyDriver[];
  vehicles: PolicyVehicle[];
  coverages: PolicyCoverage[];
  endorsements: EndorsementDto[];
}

export interface DocumentDto { id: string; policyId: string; type: string; blobUrl: string; generatedAt: string; }

export interface AccountDto { id: string; email: string; b2cObjectId: string; policyId: string; createdAt: string; }

export interface ClaimDto { id: string; policyId: string; incidentDate: string; description: string; status: string; createdAt: string; }
export interface ClaimDocumentDto { id: string; type: string; blobUrl: string; uploadedAt: string; }
export interface ClaimDetail extends ClaimDto { documents: ClaimDocumentDto[]; }

export interface PaymentTransactionDto { id: string; policyId: string; amount: number; transactionRef: string; status: string; paidAt: string | null; createdAt: string; }
export interface BillingScheduleDto { policyId: string; frequency: string; nextDueDate: string; }

export interface CoverageChangeDto { coverageTypeId: number; newLimits: string; newPremium: number; }
