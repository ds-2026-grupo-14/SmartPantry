import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Pipe, PipeTransform } from '@angular/core';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { LocalizationPipe } from '@abp/ng.core';
import {
  MAX_TERM_LENGTH,
  MAX_WATCHED_TERMS,
  PreferencesComponent,
  normalizeDietaryTerm,
} from './preferences.component';
import { DietaryPreferenceDto, DietaryPreferenceService } from '../proxy/preferences';

@Pipe({
  name: 'abpLocalization',
  standalone: true,
})
class MockLocalizationPipe implements PipeTransform {
  transform(key: string | null | undefined): string {
    return key ?? '';
  }
}

describe('PreferencesComponent (Bloque 2)', () => {
  let fixture: ComponentFixture<PreferencesComponent>;
  let component: PreferencesComponent;
  let getMyPreferencesSubject: Subject<DietaryPreferenceDto>;
  let serviceMock: {
    getMyPreferences: ReturnType<typeof vi.fn>;
    updateMyPreferences: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    getMyPreferencesSubject = new Subject<DietaryPreferenceDto>();

    serviceMock = {
      getMyPreferences: vi.fn().mockImplementation(() => getMyPreferencesSubject.asObservable()),
      updateMyPreferences: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [PreferencesComponent],
      providers: [
        provideRouter([]),
        { provide: DietaryPreferenceService, useValue: serviceMock },
      ],
    })
      .overrideComponent(PreferencesComponent, {
        remove: { imports: [LocalizationPipe] },
        add: { imports: [MockLocalizationPipe] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(PreferencesComponent);
    component = fixture.componentInstance;
  });

  it('should show loading state, ethical disclaimer (BR-001) and profile link (RF-02) while pending', () => {
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('loading');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('::Preferences:Loading');
    expect(el.textContent).not.toContain('::Preferences:Empty');
    expect(el.querySelector('.spinner-border')).not.toBeNull();
    expect(el.textContent).toContain('::Preferences:EthicalDisclaimerTitle');
    expect(el.textContent).toContain('::Preferences:EthicalDisclaimerText');

    const profileLink = el.querySelector<HTMLAnchorElement>('a.account-profile-link');
    expect(profileLink).not.toBeNull();
    expect(profileLink?.getAttribute('href')).toBe('/account/manage-profile');
  });

  it('should display error state and recover when retry() is triggered', () => {
    fixture.detectChanges();

    getMyPreferencesSubject.error(new Error('Network failure'));
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('error');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('::Preferences:ErrorLoading');

    const retrySubject = new Subject<DietaryPreferenceDto>();
    serviceMock.getMyPreferences.mockReturnValueOnce(retrySubject.asObservable());

    const retryButton = el.querySelector<HTMLButtonElement>('button.btn-outline-danger');
    expect(retryButton).not.toBeNull();
    retryButton?.click();
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('loading');
    expect(serviceMock.getMyPreferences).toHaveBeenCalledTimes(2);

    retrySubject.next({
      userId: 'user-1',
      watchedTerms: ['gluten', 'mani'],
      showAlertsOnScreen: true,
    });
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('results');
    expect(component.watchedTerms()).toEqual(['gluten', 'mani']);
  });

  it('should display empty state when user has no watched terms configured', () => {
    fixture.detectChanges();

    getMyPreferencesSubject.next({
      userId: 'user-1',
      watchedTerms: [],
      showAlertsOnScreen: true,
    });
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('empty');
    expect(component.watchedTerms()).toEqual([]);

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('::Preferences:Empty');
    expect(el.querySelectorAll('.watched-term-chip').length).toBe(0);
  });

  it('should display chips in results state and remove terms transitioning to empty', () => {
    fixture.detectChanges();

    getMyPreferencesSubject.next({
      userId: 'user-1',
      watchedTerms: ['gluten', 'lactosa'],
      showAlertsOnScreen: true,
    });
    fixture.detectChanges();

    expect(component.viewStatus()).toBe('results');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.watched-term-chip').length).toBe(2);

    component.removeTerm('gluten');
    fixture.detectChanges();
    expect(component.watchedTerms()).toEqual(['lactosa']);
    expect(component.viewStatus()).toBe('results');

    component.removeTerm('lactosa');
    fixture.detectChanges();
    expect(component.watchedTerms()).toEqual([]);
    expect(component.viewStatus()).toBe('empty');
  });

  it('should normalize accents/case when adding a term and prevent duplicates', () => {
    fixture.detectChanges();
    getMyPreferencesSubject.next({
      userId: 'user-1',
      watchedTerms: [],
      showAlertsOnScreen: true,
    });
    fixture.detectChanges();

    component.termControl.setValue('  Maní  ');
    component.addTerm();
    fixture.detectChanges();

    expect(component.watchedTerms()).toEqual(['mani']);
    expect(component.viewStatus()).toBe('results');
    expect(component.termControl.value).toBe('');
    expect(component.clientValidationError()).toBeNull();

    // Duplicado con mayúsculas y tilde -> debe ser bloqueado
    component.termControl.setValue('MANÍ');
    component.addTerm();
    fixture.detectChanges();

    expect(component.watchedTerms()).toEqual(['mani']);
    expect(component.clientValidationError()).toBe('::Preferences:Validation:DuplicateTerm');
  });

  it('should reject terms shorter than 2 characters or longer than 64 characters', () => {
    fixture.detectChanges();
    getMyPreferencesSubject.next({
      userId: 'user-1',
      watchedTerms: [],
      showAlertsOnScreen: true,
    });

    // Vacío / 1 carácter / separador
    component.termControl.setValue('a');
    component.addTerm();
    expect(component.clientValidationError()).toBe('::Preferences:Validation:TermLength');
    expect(component.watchedTerms()).toEqual([]);

    component.termControl.setValue(';');
    component.addTerm();
    expect(component.clientValidationError()).toBe('::Preferences:Validation:TermLength');

    // Mayor a 64 caracteres
    component.termControl.setValue('x'.repeat(MAX_TERM_LENGTH + 1));
    component.addTerm();
    expect(component.clientValidationError()).toBe('::Preferences:Validation:TermLength');
    expect(component.watchedTerms()).toEqual([]);
  });

  it('should enforce maximum of 30 watched terms', () => {
    fixture.detectChanges();
    const initialTerms = Array.from({ length: MAX_WATCHED_TERMS }, (_, i) => `termino${i + 1}`);
    getMyPreferencesSubject.next({
      userId: 'user-1',
      watchedTerms: initialTerms,
      showAlertsOnScreen: true,
    });

    component.termControl.setValue('termino31');
    component.addTerm();

    expect(component.watchedTerms().length).toBe(MAX_WATCHED_TERMS);
    expect(component.clientValidationError()).toBe('::Preferences:Validation:MaxTermsReached');
  });

  it('should toggle showAlertsOnScreen and save preferences successfully', () => {
    fixture.detectChanges();
    getMyPreferencesSubject.next({
      userId: 'user-1',
      watchedTerms: ['gluten'],
      showAlertsOnScreen: true,
    });
    fixture.detectChanges();

    const switchEl = fixture.nativeElement.querySelector('#show-alerts-switch') as HTMLInputElement;
    expect(switchEl.checked).toBe(true);

    switchEl.checked = false;
    switchEl.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(component.showAlertsOnScreen()).toBe(false);

    const savedDto: DietaryPreferenceDto = {
      userId: 'user-1',
      watchedTerms: ['gluten'],
      showAlertsOnScreen: false,
      lastModificationTime: '2026-11-23T20:00:00Z',
    };
    serviceMock.updateMyPreferences.mockReturnValue(of(savedDto));

    component.save();
    fixture.detectChanges();

    expect(serviceMock.updateMyPreferences).toHaveBeenCalledWith(
      { watchedTerms: ['gluten'], showAlertsOnScreen: false },
      { skipHandleError: true },
    );
    expect(component.isSaving()).toBe(false);
    expect(component.saveSuccess()).toBe(true);
    expect(component.serverError()).toBeNull();
  });

  it('should preserve edited chips and display serverError when save fails', () => {
    fixture.detectChanges();
    getMyPreferencesSubject.next({
      userId: 'user-1',
      watchedTerms: ['gluten'],
      showAlertsOnScreen: true,
    });
    fixture.detectChanges();

    component.termControl.setValue('Soja');
    component.addTerm();

    serviceMock.updateMyPreferences.mockReturnValue(throwError(() => new Error('HTTP 500')));

    component.save();
    fixture.detectChanges();

    expect(component.isSaving()).toBe(false);
    expect(component.saveSuccess()).toBe(false);
    expect(component.serverError()).toBe('::Preferences:SaveError');
    expect(component.watchedTerms()).toEqual(['gluten', 'soja']);
  });

  describe('normalizeDietaryTerm', () => {
    it('should strip accents, semicolons, excess whitespace and lowercase text', () => {
      expect(normalizeDietaryTerm('  Maní;  Tostado ')).toBe('mani tostado');
      expect(normalizeDietaryTerm('LÁCTEOS')).toBe('lacteos');
      expect(normalizeDietaryTerm('   ')).toBe('');
    });
  });
});
