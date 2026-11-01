import type { AuthorDto, AuthorExcelDownloadDto, CreateUpdateAuthorDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { DownloadTokenResultDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AuthorService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateAuthorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AuthorDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/author',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/author/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AuthorDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/author/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getDownloadToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DownloadTokenResultDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/author/download-token',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AuthorDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/author',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getListAsExcelFile = (input: AuthorExcelDownloadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      headers: { Accept: 'application/octet-stream' },
      url: '/api/app/author/as-excel-file',
      params: { downloadToken: input.downloadToken, sorting: input.sorting },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAuthorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AuthorDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/author/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}