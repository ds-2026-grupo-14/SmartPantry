import { mapEnumToOptions } from '@abp/ng.core';

export enum ItemCategory {
  Undefined = 0,
  Dairy = 1,
  Bakery = 2,
  Beverages = 3,
  CannedGoods = 4,
  DryGoods = 5,
  FruitsAndVegetables = 6,
  MeatAndFish = 7,
  Snacks = 8,
  Condiments = 9,
  Frozen = 10,
  Cleaning = 11,
  Other = 12,
}

export const itemCategoryOptions = mapEnumToOptions(ItemCategory);
