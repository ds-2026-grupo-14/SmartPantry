import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { DietaryVerificationStatus } from '../../../proxy/preferences';

@Component({
  selector: 'app-dietary-alert',
  templateUrl: './dietary-alert.component.html',
  styleUrl: './dietary-alert.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
})
export class DietaryAlertComponent {
  readonly status = input<DietaryVerificationStatus>(DietaryVerificationStatus.NotConfigured);
  readonly matchedTerms = input<readonly string[]>([]);
  readonly informationalMessage = input<string | null | undefined>(null);
  readonly compact = input<boolean>(false);

  readonly DietaryVerificationStatus = DietaryVerificationStatus;
}
