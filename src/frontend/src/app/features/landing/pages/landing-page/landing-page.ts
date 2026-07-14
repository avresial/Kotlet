import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ThemeService } from '../../../../core/theme.service';

@Component({
  selector: 'app-landing-page',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPage {
  constructor() {
    // The landing page is a light-only marketing surface, so pin the light
    // palette while it is shown to keep the header and page consistent.
    const theme = inject(ThemeService);
    theme.forceLight(true);
    inject(DestroyRef).onDestroy(() => theme.forceLight(false));
  }
}
