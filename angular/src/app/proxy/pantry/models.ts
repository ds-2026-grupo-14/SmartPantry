import type { AuditedEntityDto } from '@abp/ng.core';
import type { PantryItemState } from './pantry-item-state.enum';

export interface CreatePantryItemDto {
  productId: string;
  quantity?: number | null;
  unit?: string | null;
  expirationDate?: string | null;
}

export interface PantryItemDto extends AuditedEntityDto<string> {
  userId?: string;
  productId?: string;
  productName?: string;
  quantity?: number | null;
  unit?: string | null;
  expirationDate?: string | null;
  state?: PantryItemState;
}

export interface UpdatePantryItemDto {
  quantity?: number | null;
  unit?: string | null;
  expirationDate?: string | null;
}
