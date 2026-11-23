import { mapEnumToOptions } from '@abp/ng.core';

export enum DietaryVerificationStatus {
  Compatible = 0,
  Warning = 1,
  Unverifiable = 2,
  NotConfigured = 3,
}

export const dietaryVerificationStatusOptions = mapEnumToOptions(DietaryVerificationStatus);
