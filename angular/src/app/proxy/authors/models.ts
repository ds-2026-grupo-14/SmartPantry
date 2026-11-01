import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface AuthorDto extends FullAuditedEntityDto<string> {
  name?: string;
  birthDate?: string;
  shortBio?: string | null;
}

export interface AuthorExcelDownloadDto {
  downloadToken?: string;
  sorting?: string | null;
}

export interface CreateUpdateAuthorDto {
  name: string;
  birthDate: string;
  shortBio?: string | null;
}
