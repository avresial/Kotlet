import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of, Subject, switchMap } from 'rxjs';
import { getApiError } from '../../../../core/http/api-error';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { PantryRecipeMatch } from '../../pantry.models';
import { PantryService } from '../../pantry.service';

@Component({
  selector: 'app-pantry-recipe-matches', imports: [RouterLink, TranslatePipe],
  templateUrl: './pantry-recipe-matches.html', styleUrl: './pantry-recipe-matches.scss', changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PantryRecipeMatches implements OnInit {
  private readonly pantryService = inject(PantryService);
  private readonly translations = inject(TranslationService);
  private readonly reload$ = new Subject<void>();
  readonly matches = signal<PantryRecipeMatch[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);

  constructor() {
    // switchMap drops stale in-flight requests when the pantry changes again quickly.
    this.reload$.pipe(
      switchMap(() => this.pantryService.getRecipeMatches().pipe(
        catchError(error => { this.error.set(getApiError(error, this.translations.translate('pantry.matches.error'))); return of(null); }),
      )),
      takeUntilDestroyed(),
    ).subscribe(matches => { this.isLoading.set(false); if (matches) this.matches.set(matches); });
  }

  ngOnInit(): void { this.reload(); }

  reload(): void { this.isLoading.set(true); this.error.set(null); this.reload$.next(); }

  missingNames(match: PantryRecipeMatch): string { return match.missingIngredients.map(ingredient => ingredient.name).join(', '); }
  matchPercent(match: PantryRecipeMatch): number { return match.totalIngredientCount > 0 ? Math.round(100 * match.matchedIngredientCount / match.totalIngredientCount) : 0; }
}
