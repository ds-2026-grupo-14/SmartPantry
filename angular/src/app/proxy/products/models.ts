import type { ItemCategory } from './item-category.enum';
import type { EntityDto } from '@abp/ng.core';

export interface CreateProductDto {
  barcode: string;
  name: string;
  category: ItemCategory;
  brand?: string | null;
  quantity?: string | null;
  ingredients?: string | null;
  allergens?: string | null;
}

export interface ProductDto extends EntityDto<string> {
  barcode?: string;
  name?: string;
  category?: ItemCategory;
  brand?: string | null;
  quantity?: string | null;
  ingredients?: string | null;
  allergens?: string | null;
}

export interface UpdateProductDto {
  name: string;
  category: ItemCategory;
  brand?: string | null;
  quantity?: string | null;
  ingredients?: string | null;
  allergens?: string | null;
}
