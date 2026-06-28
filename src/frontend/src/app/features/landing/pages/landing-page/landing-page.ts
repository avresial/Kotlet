import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-landing-page',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPage {}
