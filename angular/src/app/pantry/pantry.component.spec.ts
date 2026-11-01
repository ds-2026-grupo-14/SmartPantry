import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, Directive, EventEmitter, Input, Output, Pipe, PipeTransform } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Observable, Subject, of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService, ModalCloseDirective, ModalComponent } from '@abp/ng.theme.shared';
import { LIST_QUERY_DEBOUNCE_TIME, LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import { PantryComponent, calendarDateValidator } from './pantry.component';
import { PantryItemDto, PantryItemState, PantryService, UpdatePantryItemDto } from '../proxy/pantry';

@Pipe({
  name: 'abpLocalization',
  standalone: true,
})
class MockLocalizationPipe implements PipeTransform {
  transform(key: string | null | undefined): string {
    return key ?? '';
  }
}

@Component({
  selector: 'abp-modal',
  standalone: true,
  template: `
    @if (visible) {
      <div class="mock-modal">
        <ng-content select="[abpHeader]" />
        <ng-content select="[abpBody]" />
        <ng-content select="[abpFooter]" />
      </div>
    }
  `,
})
class MockModalComponent {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
}

@Directive({
  selector: '[abpClose]',
  standalone: true,
})
class MockModalCloseDirective {}

describe('PantryComponent (Capa 7)', () => {
  let fixture: ComponentFixture<PantryComponent>;
  let component: PantryComponent;
  let pantryServiceMock: {
    getList: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    consume: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
  };
  let confirmationServiceMock: {
    warn: ReturnType<typeof vi.fn>;
  };

  let getListSubject: Subject<PagedResultDto<PantryItemDto>>;

  const sampleItems: PantryItemDto[] = [
    {
      id: 'item-1',
      userId: 'user-1',
      productId: 'prod-1',
      productName: 'Leche Entera',
      quantity: 2,
      unit: 'litros',
      expirationDate: '2026-11-15T00:00:00Z',
      state: PantryItemState.Active,
    },
    {
      id: 'item-2',
      userId: 'user-1',
      productId: 'prod-2',
      productName: 'Yogur Natural',
      quantity: null,
      unit: null,
      expirationDate: null,
      state: PantryItemState.Active,
    },
    {
      id: 'item-3',
      userId: 'user-1',
      productId: 'prod-3',
      productName: 'Fideos Tallarines',
      quantity: 500,
      unit: 'g',
      expirationDate: '2026-10-10T00:00:00Z',
      state: PantryItemState.Consumed,
    },
  ];

  beforeEach(async () => {
    getListSubject = new Subject<PagedResultDto<PantryItemDto>>();

    pantryServiceMock = {
      getList: vi.fn().mockImplementation(() => getListSubject.asObservable()),
      update: vi.fn(),
      consume: vi.fn(),
      delete: vi.fn(),
    };

    confirmationServiceMock = {
      warn: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [PantryComponent],
      providers: [
        { provide: LIST_QUERY_DEBOUNCE_TIME, useValue: 0 },
        { provide: PantryService, useValue: pantryServiceMock },
        { provide: ConfirmationService, useValue: confirmationServiceMock },
      ],
    })
      .overrideComponent(PantryComponent, {
        remove: {
          imports: [LocalizationPipe, ModalComponent, ModalCloseDirective],
        },
        add: {
          imports: [MockLocalizationPipe, MockModalComponent, MockModalCloseDirective],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(PantryComponent);
    component = fixture.componentInstance;
  });

  // 1. Loading state & NOT empty
  it('should show loading state and NOT display empty message while request is pending', () => {
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('loading');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('::Pantry:Loading');
    expect(compiled.textContent).not.toContain('::Pantry:Empty');
    expect(compiled.querySelector('.spinner-border')).not.toBeNull();
  });

  // 2. Error state & Retry
  it('should display error state and retry successfully', () => {
    fixture.detectChanges();

    getListSubject.error(new Error('Network error'));
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('error');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('::Pantry:ErrorLoading');
    expect(compiled.textContent).toContain('::Pantry:Retry');

    const retryCallCountBefore = pantryServiceMock.getList.mock.calls.length;

    // Simular nueva respuesta exitosa tras reintento
    const retrySubject = new Subject<PagedResultDto<PantryItemDto>>();
    pantryServiceMock.getList.mockReturnValueOnce(retrySubject.asObservable());

    const retryBtn = compiled.querySelector<HTMLButtonElement>('button.btn-outline-danger');
    expect(retryBtn).not.toBeNull();
    retryBtn?.click();
    fixture.detectChanges();

    expect(pantryServiceMock.getList.mock.calls.length).toBeGreaterThan(retryCallCountBefore);
    expect(component.viewStatus()).toBe('loading');

    retrySubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('results');
    expect(component.pantryItems().length).toBe(3);
  });

  // 3. Empty state
  it('should show empty state when items list is empty', () => {
    fixture.detectChanges();

    getListSubject.next({ items: [], totalCount: 0 });
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('empty');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('::Pantry:Empty');
    expect(compiled.querySelector('.pantry-table')).toBeNull();
  });

  // 4. Results & RF-09 (missing fields handled with 'Not specified')
  it('should display items and handle missing fields with "Not specified" (RF-09)', () => {
    fixture.detectChanges();

    getListSubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('results');
    expect(component.pantryItems().length).toBe(3);

    const compiled = fixture.nativeElement as HTMLElement;

    // Nombres de productos
    expect(compiled.textContent).toContain('Leche Entera');
    expect(compiled.textContent).toContain('Yogur Natural');
    expect(compiled.textContent).toContain('Fideos Tallarines');

    // RF-09: El ítem 2 tiene cantidad, unidad y fecha nulas -> debe mostrar '::Pantry:NotSpecified'
    expect(compiled.textContent).toContain('::Pantry:NotSpecified');

    // Estados visuales diferenciados
    expect(compiled.textContent).toContain('::Pantry:State:Active');
    expect(compiled.textContent).toContain('::Pantry:State:Consumed');
  });

  // 5. Validation in form: negative quantity or invalid date prevents update call
  it('should not call update service when form has negative quantity or invalid date', () => {
    fixture.detectChanges();
    getListSubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    // Abrir formulario de edición para el primer ítem
    component.edit(sampleItems[0]);
    fixture.detectChanges();

    expect(component.isModalOpen()).toBe(true);

    // Caso A: Cantidad negativa (-2)
    component.form().patchValue({ quantity: -2 });
    expect(component.form().invalid).toBe(true);
    expect(component.form().get('quantity')?.hasError('min')).toBe(true);

    component.save();
    expect(pantryServiceMock.update).not.toHaveBeenCalled();

    // Caso B: Fecha de calendario inválida
    component.form().patchValue({ quantity: 5, expirationDate: '2026-02-31' });
    expect(component.form().invalid).toBe(true);
    expect(component.form().get('expirationDate')?.hasError('invalidDate')).toBe(true);

    component.save();
    expect(pantryServiceMock.update).not.toHaveBeenCalled();
  });

  // 6. Delete confirmation accepted
  it('should call delete service when confirmation is accepted', () => {
    fixture.detectChanges();
    getListSubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    confirmationServiceMock.warn.mockReturnValue(of(Confirmation.Status.confirm));
    pantryServiceMock.delete.mockReturnValue(of(void 0));

    component.delete(sampleItems[0]);

    expect(confirmationServiceMock.warn).toHaveBeenCalledWith(
      '::Pantry:DeleteConfirmationMessage',
      '::Pantry:DeleteConfirmationTitle',
    );
    expect(pantryServiceMock.delete).toHaveBeenCalledWith('item-1');
  });

  // 7. Delete confirmation cancelled
  it('should NOT call delete service when confirmation is cancelled', () => {
    fixture.detectChanges();
    getListSubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    confirmationServiceMock.warn.mockReturnValue(of(Confirmation.Status.reject));

    component.delete(sampleItems[0]);

    expect(confirmationServiceMock.warn).toHaveBeenCalledWith(
      '::Pantry:DeleteConfirmationMessage',
      '::Pantry:DeleteConfirmationTitle',
    );
    expect(pantryServiceMock.delete).not.toHaveBeenCalled();
  });

  // 8. Successful update closes modal and updates item
  it('should update item and close modal when form is valid on save', () => {
    fixture.detectChanges();
    getListSubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    component.edit(sampleItems[0]);
    component.form().patchValue({
      quantity: 5,
      unit: 'litros',
      expirationDate: '2026-12-01',
    });

    const updatedItem: PantryItemDto = {
      ...sampleItems[0],
      quantity: 5,
      expirationDate: '2026-12-01T00:00:00Z',
    };
    pantryServiceMock.update.mockReturnValue(of(updatedItem));

    component.save();

    expect(pantryServiceMock.update).toHaveBeenCalledWith(
      'item-1',
      {
        quantity: 5,
        unit: 'litros',
        expirationDate: '2026-12-01',
      },
      { skipHandleError: true },
    );
    expect(component.isModalOpen()).toBe(false);
    expect(component.pantryItems().find(i => i.id === 'item-1')?.quantity).toBe(5);
  });

  // 9. Error on save retains form values and sets serverError
  it('should retain form values and set serverError when save fails', () => {
    fixture.detectChanges();
    getListSubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    component.edit(sampleItems[0]);
    component.form().patchValue({
      quantity: 10,
      unit: 'cajas',
      expirationDate: '2026-12-10',
    });

    pantryServiceMock.update.mockReturnValue(throwError(() => new Error('Server error')));

    component.save();

    expect(component.isSaving()).toBe(false);
    expect(component.isModalOpen()).toBe(true);
    expect(component.serverError()).toBe('::Pantry:SaveError');
    expect(component.form().value.quantity).toBe(10);
    expect(component.form().value.unit).toBe('cajas');
  });

  // 10. Mark as consumed updates state and preserves item (Regla de Negocio 6)
  it('should mark item as consumed and preserve record in list (Regla de Negocio 6)', () => {
    fixture.detectChanges();
    getListSubject.next({ items: sampleItems, totalCount: sampleItems.length });
    fixture.detectChanges();

    const consumedItem: PantryItemDto = {
      ...sampleItems[0],
      state: PantryItemState.Consumed,
    };
    pantryServiceMock.consume.mockReturnValue(of(consumedItem));

    component.markAsConsumed(sampleItems[0]);

    expect(pantryServiceMock.consume).toHaveBeenCalledWith('item-1');
    const itemInState = component.pantryItems().find(i => i.id === 'item-1');
    expect(itemInState).toBeDefined();
    expect(itemInState?.state).toBe(PantryItemState.Consumed);
    expect(component.pantryItems().length).toBe(3);
  });

  // 11. calendarDateValidator logic
  describe('calendarDateValidator', () => {
    it('should validate valid calendar dates', () => {
      expect(calendarDateValidator(new FormControl('2026-05-15'))).toBeNull();
      expect(calendarDateValidator(new FormControl('1999-12-31'))).toBeNull();
      expect(calendarDateValidator(new FormControl('2024-02-29'))).toBeNull(); // bisiesto
      expect(calendarDateValidator(new FormControl(null))).toBeNull();
      expect(calendarDateValidator(new FormControl(''))).toBeNull();
    });

    it('should reject invalid calendar dates and out of range years', () => {
      expect(calendarDateValidator(new FormControl('2026-02-29'))).toEqual({ invalidDate: true }); // no bisiesto
      expect(calendarDateValidator(new FormControl('2026-04-31'))).toEqual({ invalidDate: true }); // abril tiene 30
      expect(calendarDateValidator(new FormControl('1899-12-31'))).toEqual({ invalidDate: true }); // < 1900
      expect(calendarDateValidator(new FormControl('2101-01-01'))).toEqual({ invalidDate: true }); // > 2100
      expect(calendarDateValidator(new FormControl('not-a-date'))).toEqual({ invalidDate: true });
    });
  });
});
