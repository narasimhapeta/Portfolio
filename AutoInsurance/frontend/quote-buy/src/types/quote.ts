export interface PersonalInfo {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  email: string;
  phone: string;
  street: string;
  city: string;
  state: string;
  zipCode: string;
}

export interface Driver {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  licenseNumber: string;
  licenseState: string;
  isPrimary: boolean;
}

export interface Vehicle {
  year: number;
  make: string;
  model: string;
  vin: string;
  primaryUse: string;
}

export interface CoverageType {
  id: number;
  name: string;
  description: string;
  mockAnnualRate: number;
}

export interface SelectedCoverage {
  coverageTypeId: number;
  limits: string;
}

export interface QuoteSession {
  quoteId: string;
  quoteNumber: string;
  sessionToken: string;
  zipCode: string;
  stepReached: number;
}

export interface ReviewData {
  quoteId: string;
  quoteNumber: string;
  annualPremium: number;
  drivers: Driver[];
  vehicles: Vehicle[];
  coverages: { name: string; limits: string; annualPremium: number }[];
}

export interface PolicyBound {
  policyId: string;
  policyNumber: string;
}
