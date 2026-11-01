import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgbPaginationModule } from '@ng-bootstrap/ng-bootstrap';
import { catchError, of, switchMap, tap } from 'rxjs';
import { ListService, LocalizationPipe } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ModalCloseDirective,
  ModalComponent,
} from '@abp/ng.theme.shared';
import { PantryItemDto, PantryItemState, PantryService, UpdatePantryItemDto } from '../proxy/pantry';

export type PantryViewStatus = 'loading' | 'error' | 'empty' | 'results';

const MAX_UNIT_LENGTH = 32;
const MIN_YEAR = 1900;
const MAX_YEAR = 2100;

/** Valida que el valor (yyyy-MM-dd) sea una fecha de calendario real y razonable. */
export function calendarDateValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string | null;
  if (!value) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) {
    return { invalidDate: true };
  }
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const date = new Date(year, month - 1, day);
  const isReal =
    date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day;
  return isReal && year >= MIN_YEAR && year <= MAX_YEAR ? null : { invalidDate: true };
}

@Component({
  selector: 'app-pantry',
  templateUrl: './pantry.component.html',
  styleUrl: './pantry.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    NgbPaginationModule,
    ModalComponent,
    ModalCloseDirective,
    LocalizationPipe,
    DatePipe,
  ],
  providers: [ListService],
})
export class PantryComponent {
  readonly list = inject(ListService);
  private readonly pantryService = inject(PantryService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly PantryItemState = PantryItemState;
  readonly maxUnitLength = MAX_UNIT_LENGTH;

  viewStatus = signal<PantryViewStatus>('loading');
  pantryItems = signal<PantryItemDto[]>([]);
  totalCount = signal(0);
  selectedItem = signal<PantryItemDto | undefined>(undefined);
  form = signal<FormGroup>(new FormGroup({}));
  isModalOpen = signal(false);
  isSaving = signal(false);
  serverError = signal<string | null>(null);

  constructor() {
    this.list.maxResultCount = 10;

    this.list.query$
      .pipe(
        tap(() => this.viewStatus.set('loading')),
        switchMap(query =>
          this.pantryService.getList(query as any, { skipHandleError: true }).pipe(
            tap(res => {
              this.pantryItems.set(res.items ?? []);
              this.totalCount.set(res.totalCount ?? 0);
              this.viewStatus.set((res.totalCount ?? 0) > 0 ? 'results' : 'empty');
            }),
            catchError(() => {
              this.viewStatus.set('error');
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();

    this.list.get();
  }

  get currentPage(): number {
    return this.list.page + 1;
  }

  get pageSize(): number {
    return this.list.maxResultCount;
  }

  onPageChange(page: number) {
    this.list.page = page - 1;
  }

  retry() {
    this.list.get();
  }

  isActive(item: PantryItemDto): boolean {
    return item.state === PantryItemState.Active;
  }

  stateKey(item: PantryItemDto): string {
    switch (item.state) {
      case PantryItemState.Consumed:
        return '::Pantry:State:Consumed';
      case PantryItemState.Discarded:
        return '::Pantry:State:Discarded';
      default:
        return '::Pantry:State:Active';
    }
  }

  hasValue(value: unknown): boolean {
    return value !== null && value !== undefined && value !== '';
  }

  edit(item: PantryItemDto) {
    this.selectedItem.set(item);
    this.serverError.set(null);
    this.form.set(
      this.fb.group({
        quantity: [item.quantity ?? null, [Validators.min(0)]],
        unit: [item.unit ?? '', [Validators.maxLength(MAX_UNIT_LENGTH)]],
        expirationDate: [
          item.expirationDate ? item.expirationDate.substring(0, 10) : null,
          [calendarDateValidator],
        ],
      }),
    );
    this.isModalOpen.set(true);
  }

  save() {
    const form = this.form();
    const item = this.selectedItem();
    if (!item?.id || form.invalid || this.isSaving()) {
      form.markAllAsTouched();
      return;
    }

    const value = form.value;
    const input: UpdatePantryItemDto = {
      quantity: this.hasValue(value.quantity) ? Number(value.quantity) : null,
      unit: value.unit?.trim() ? value.unit.trim() : null,
      expirationDate: value.expirationDate || null,
    };

    this.isSaving.set(true);
    this.serverError.set(null);
    this.pantryService.update(item.id, input, { skipHandleError: true }).subscribe({
      next: updated => {
        this.pantryItems.update(items => items.map(i => (i.id === updated.id ? updated : i)));
        this.isSaving.set(false);
        this.isModalOpen.set(false);
      },
      error: () => {
        // Se conservan los datos ingresados en el formulario ante el rechazo del servidor.
        this.isSaving.set(false);
        this.serverError.set('::Pantry:SaveError');
      },
    });
  }

  markAsConsumed(item: PantryItemDto) {
    if (!item.id) {
      return;
    }
    this.pantryService.consume(item.id).subscribe(updated => {
      // El registro se conserva como historial (Regla de Negocio 6); solo cambia su estado.
      this.pantryItems.update(items => items.map(i => (i.id === updated.id ? updated : i)));
    });
  }

  delete(item: PantryItemDto) {
    if (!item.id) {
      return;
    }
    const id = item.id;
    this.confirmation
      .warn('::Pantry:DeleteConfirmationMessage', '::Pantry:DeleteConfirmationTitle')
      .subscribe(status => {
        if (status === Confirmation.Status.confirm) {
          this.pantryService.delete(id).subscribe(() => {
            if (this.pantryItems().length === 1 && this.list.page > 0) {
              this.list.page = this.list.page - 1;
            } else {
              this.list.get();
            }
          });
        }
      });
  }
}
