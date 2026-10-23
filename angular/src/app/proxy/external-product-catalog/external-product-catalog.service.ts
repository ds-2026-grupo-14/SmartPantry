import type { ExternalProductResultDto, GetExternalProductByBarcodeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ExternalProductCatalogService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getByBarcode = (input: GetExternalProductByBarcodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExternalProductResultDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/external-product-catalog/by-barcode',
      params: { barcode: input.barcode },
    },
    { apiName: this.apiName,...config });
}