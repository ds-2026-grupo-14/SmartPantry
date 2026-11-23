import type { DietaryEvaluationDto, DietaryPreferenceDto, EvaluateProductDietaryDto, UpdateDietaryPreferenceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DietaryPreferenceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  evaluateProduct = (input: EvaluateProductDietaryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DietaryEvaluationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/dietary-preference/evaluate-product',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getMyPreferences = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DietaryPreferenceDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/dietary-preference/my-preferences',
    },
    { apiName: this.apiName,...config });
  

  updateMyPreferences = (input: UpdateDietaryPreferenceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DietaryPreferenceDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: '/api/app/dietary-preference/my-preferences',
      body: input,
    },
    { apiName: this.apiName,...config });
}