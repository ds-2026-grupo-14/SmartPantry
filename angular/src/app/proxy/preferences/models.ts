import type { DietaryVerificationStatus } from './dietary-verification-status.enum';

export interface DietaryEvaluationDto {
  verificationStatus?: DietaryVerificationStatus;
  matchedTerms?: string[];
  informationalMessage?: string | null;
}

export interface DietaryPreferenceDto {
  userId?: string;
  watchedTerms?: string[];
  showAlertsOnScreen?: boolean;
  lastModificationTime?: string | null;
}

export interface EvaluateProductDietaryDto {
  ingredients?: string | null;
  allergens?: string | null;
}

export interface UpdateDietaryPreferenceDto {
  watchedTerms?: string[];
  showAlertsOnScreen?: boolean;
}
