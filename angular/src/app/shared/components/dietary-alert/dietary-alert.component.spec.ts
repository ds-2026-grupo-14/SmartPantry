import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Pipe, PipeTransform } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { DietaryVerificationStatus } from '../../../proxy/preferences';
import { DietaryAlertComponent } from './dietary-alert.component';

@Pipe({
  name: 'abpLocalization',
  standalone: true,
})
class MockLocalizationPipe implements PipeTransform {
  transform(key: string | null | undefined): string {
    return key ?? '';
  }
}

describe('DietaryAlertComponent (Bloque 2)', () => {
  let fixture: ComponentFixture<DietaryAlertComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DietaryAlertComponent],
    })
      .overrideComponent(DietaryAlertComponent, {
        remove: { imports: [LocalizationPipe] },
        add: { imports: [MockLocalizationPipe] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(DietaryAlertComponent);
  });

  it('should render nothing when status is NotConfigured', () => {
    fixture.componentRef.setInput('status', DietaryVerificationStatus.NotConfigured);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.alert')).toBeNull();
    expect(el.querySelector('.badge')).toBeNull();
  });

  it('should render Warning banner with matched terms and ethical source attribution (BR-001)', () => {
    fixture.componentRef.setInput('status', DietaryVerificationStatus.Warning);
    fixture.componentRef.setInput('matchedTerms', ['gluten', 'leche']);
    fixture.componentRef.setInput('informationalMessage', null);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.alert-warning')).not.toBeNull();
    expect(el.textContent).toContain('::DietaryAlert:WarningTitle');
    expect(el.textContent).toContain('::DietaryAlert:WarningMessage');
    expect(el.textContent).toContain('::DietaryAlert:SourceAttribution');

    const termChips = Array.from(el.querySelectorAll('.dietary-matched-term')).map(n =>
      n.textContent?.trim(),
    );
    expect(termChips).toEqual(['gluten', 'leche']);
  });

  it('should render Unverifiable banner when product data is incomplete (BR-002)', () => {
    fixture.componentRef.setInput('status', DietaryVerificationStatus.Unverifiable);
    fixture.componentRef.setInput('matchedTerms', []);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.alert-secondary')).not.toBeNull();
    expect(el.textContent).toContain('::DietaryAlert:UnverifiableTitle');
    expect(el.textContent).toContain('::DietaryAlert:UnverifiableMessage');
    expect(el.textContent).toContain('::DietaryAlert:SourceAttribution');
  });

  it('should render Compatible banner when product has full data and no matches', () => {
    fixture.componentRef.setInput('status', DietaryVerificationStatus.Compatible);
    fixture.componentRef.setInput('informationalMessage', 'Sin coincidencias personalizadas.');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.alert-success')).not.toBeNull();
    expect(el.textContent).toContain('::DietaryAlert:CompatibleTitle');
    expect(el.textContent).toContain('Sin coincidencias personalizadas.');
  });

  it('should render compact badges when compact mode is enabled', () => {
    fixture.componentRef.setInput('compact', true);

    fixture.componentRef.setInput('status', DietaryVerificationStatus.Warning);
    fixture.componentRef.setInput('matchedTerms', ['mani']);
    fixture.detectChanges();
    let el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.badge.bg-warning')).not.toBeNull();
    expect(el.textContent).toContain('mani');

    fixture.componentRef.setInput('status', DietaryVerificationStatus.Unverifiable);
    fixture.detectChanges();
    el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.badge.bg-secondary')).not.toBeNull();

    fixture.componentRef.setInput('status', DietaryVerificationStatus.Compatible);
    fixture.detectChanges();
    el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.badge.bg-success')).not.toBeNull();
  });
});
