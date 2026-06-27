import { createSlice } from '@reduxjs/toolkit';
import type { PayloadAction } from '@reduxjs/toolkit';
import type { Driver, PersonalInfo, PolicyBound, QuoteSession, ReviewData, SelectedCoverage, Vehicle } from '../types/quote';

export interface QuoteState {
  session: QuoteSession | null;
  personalInfo: PersonalInfo | null;
  drivers: Driver[];
  vehicles: Vehicle[];
  coverages: SelectedCoverage[];
  review: ReviewData | null;
  policy: PolicyBound | null;
  currentStep: number;
}

const initialState: QuoteState = {
  session: null,
  personalInfo: null,
  drivers: [],
  vehicles: [],
  coverages: [],
  review: null,
  policy: null,
  currentStep: 1,
};

const quoteSlice = createSlice({
  name: 'quote',
  initialState,
  reducers: {
    setSession(state, action: PayloadAction<QuoteSession>) {
      state.session = action.payload;
    },
    setPersonalInfo(state, action: PayloadAction<PersonalInfo>) {
      state.personalInfo = action.payload;
    },
    setDrivers(state, action: PayloadAction<Driver[]>) {
      state.drivers = action.payload;
    },
    setVehicles(state, action: PayloadAction<Vehicle[]>) {
      state.vehicles = action.payload;
    },
    setCoverages(state, action: PayloadAction<SelectedCoverage[]>) {
      state.coverages = action.payload;
    },
    setReview(state, action: PayloadAction<ReviewData>) {
      state.review = action.payload;
    },
    setPolicy(state, action: PayloadAction<PolicyBound>) {
      state.policy = action.payload;
    },
    setStep(state, action: PayloadAction<number>) {
      state.currentStep = action.payload;
    },
    resetQuote() {
      return initialState;
    },
  },
});

export const { setSession, setPersonalInfo, setDrivers, setVehicles, setCoverages, setReview, setPolicy, setStep, resetQuote } = quoteSlice.actions;
export default quoteSlice.reducer;
