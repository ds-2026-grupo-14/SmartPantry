import type { ExternalProductQueryStatus } from './external-product-query-status.enum';

export interface ExternalProductDto {
  barcode?: string;
  name?: string | null;
  category?: string | null;
  brand?: string | null;
  quantity?: string | null;
  ingredients?: string | null;
  allergens?: string | null;
}

export interface ExternalProductResultDto {
  status?: ExternalProductQueryStatus;
  product?: ExternalProductDto | null;
}

export interface GetExternalProductByBarcodeDto {
  barcode: string;
}
