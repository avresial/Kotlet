import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Robot icon marking AI-assisted content; the tooltip explains the AI involvement. */
@Component({
  selector: 'app-ai-badge',
  templateUrl: './ai-badge.html',
  styleUrl: './ai-badge.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiBadge {
  /** Already-translated text shown as tooltip and read by screen readers. */
  readonly tooltip = input.required<string>();
}
