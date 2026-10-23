import { mapEnumToOptions } from '@abp/ng.core';

export enum ExternalProductQueryStatus {
  Found = 0,
  NotFound = 1,
  RateLimitExceeded = 2,
  ServiceUnavailable = 3,
}

export const externalProductQueryStatusOptions = mapEnumToOptions(ExternalProductQueryStatus);
