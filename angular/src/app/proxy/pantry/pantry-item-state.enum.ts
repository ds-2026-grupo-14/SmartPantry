import { mapEnumToOptions } from '@abp/ng.core';

export enum PantryItemState {
  Active = 0,
  Consumed = 1,
  Discarded = 2,
}

export const pantryItemStateOptions = mapEnumToOptions(PantryItemState);
