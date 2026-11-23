import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LocalizationPipe } from '@abp/ng.core';
import { DietaryPreferenceService, UpdateDietaryPreferenceDto } from '../proxy/preferences';

export type PreferencesViewStatus = 'loading' | 'error' | 'empty' | 'results';

export const MAX_WATCHED_TERMS = 30;
export const MIN_TERM_LENGTH = 2;
export const MAX_TERM_LENGTH = 64;

/** Normaliza un término a minúsculas sin acentos ni espacios redundantes, replicando la regla de dominio. */
export function normalizeDietaryTerm(value: string): string {
  if (!value) {
    return '';
  }

  return value
    .replaceAll(';', '')
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .normalize('NFC');
}

@Component({
  selector: 'app-preferences',
  templateUrl: './preferences.component.html',
  styleUrl: './preferences.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, LocalizationPipe],
})
export class PreferencesComponent {
  private readonly dietaryPreferenceService = inject(DietaryPreferenceService);
  private readonly destroyRef = inject(DestroyRef);

  readonly maxWatchedTerms = MAX_WATCHED_TERMS;
  readonly minTermLength = MIN_TERM_LENGTH;
  readonly maxTermLength = MAX_TERM_LENGTH;

  readonly viewStatus = signal<PreferencesViewStatus>('loading');
  readonly watchedTerms = signal<string[]>([]);
  readonly showAlertsOnScreen = signal<boolean>(true);
  readonly isSaving = signal<boolean>(false);
  readonly serverError = signal<string | null>(null);
  readonly clientValidationError = signal<string | null>(null);
  readonly saveSuccess = signal<boolean>(false);

  readonly termControl = new FormControl<string>('', { nonNullable: true });

  constructor() {
    this.loadPreferences();
  }

  loadPreferences(): void {
    this.viewStatus.set('loading');
    this.serverError.set(null);
    this.clientValidationError.set(null);
    this.saveSuccess.set(false);

    this.dietaryPreferenceService
      .getMyPreferences({ skipHandleError: true })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          const terms = [...(res.watchedTerms ?? [])];
          this.watchedTerms.set(terms);
          this.showAlertsOnScreen.set(res.showAlertsOnScreen ?? true);
          this.viewStatus.set(terms.length > 0 ? 'results' : 'empty');
        },
        error: () => {
          this.viewStatus.set('error');
        },
      });
  }

  retry(): void {
    this.loadPreferences();
  }

  addTerm(): void {
    this.saveSuccess.set(false);
    this.serverError.set(null);

    const normalized = normalizeDietaryTerm(this.termControl.value);

    if (normalized.length < MIN_TERM_LENGTH || normalized.length > MAX_TERM_LENGTH) {
      this.clientValidationError.set('::Preferences:Validation:TermLength');
      return;
    }

    const currentTerms = this.watchedTerms();

    if (currentTerms.includes(normalized)) {
      this.clientValidationError.set('::Preferences:Validation:DuplicateTerm');
      return;
    }

    if (currentTerms.length >= MAX_WATCHED_TERMS) {
      this.clientValidationError.set('::Preferences:Validation:MaxTermsReached');
      return;
    }

    this.clientValidationError.set(null);
    this.watchedTerms.set([...currentTerms, normalized]);
    this.termControl.setValue('');
    this.viewStatus.set('results');
  }

  removeTerm(term: string): void {
    this.saveSuccess.set(false);
    this.serverError.set(null);
    this.clientValidationError.set(null);

    const nextTerms = this.watchedTerms().filter(t => t !== term);
    this.watchedTerms.set(nextTerms);
    this.viewStatus.set(nextTerms.length > 0 ? 'results' : 'empty');
  }

  onToggleAlertsEvent(event: Event): void {
    const checkbox = event.target as HTMLInputElement;
    this.setShowAlertsOnScreen(checkbox.checked);
  }

  setShowAlertsOnScreen(enabled: boolean): void {
    this.showAlertsOnScreen.set(enabled);
    this.saveSuccess.set(false);
  }

  save(): void {
    const status = this.viewStatus();
    if (this.isSaving() || status === 'loading' || status === 'error') {
      return;
    }

    const input: UpdateDietaryPreferenceDto = {
      watchedTerms: [...this.watchedTerms()],
      showAlertsOnScreen: this.showAlertsOnScreen(),
    };

    this.isSaving.set(true);
    this.serverError.set(null);
    this.saveSuccess.set(false);

    this.dietaryPreferenceService
      .updateMyPreferences(input, { skipHandleError: true })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updated => {
          const savedTerms = [...(updated.watchedTerms ?? [])];
          this.watchedTerms.set(savedTerms);
          this.showAlertsOnScreen.set(updated.showAlertsOnScreen ?? true);
          this.viewStatus.set(savedTerms.length > 0 ? 'results' : 'empty');
          this.isSaving.set(false);
          this.saveSuccess.set(true);
        },
        error: () => {
          this.isSaving.set(false);
          this.serverError.set('::Preferences:SaveError');
        },
      });
  }
}
