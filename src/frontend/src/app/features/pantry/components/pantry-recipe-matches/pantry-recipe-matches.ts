import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
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
  readonly matches = signal<PantryRecipeMatch[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.isLoading.set(true); this.error.set(null);
    this.pantryService.getRecipeMatches().pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: matches => this.matches.set(matches),
      error: error => this.error.set(getApiError(error, this.translations.translate('pantry.matches.error'))),
    });
  }

  missingNames(match: PantryRecipeMatch): string { return match.missingIngredients.map(ingredient => ingredient.name).join(', '); }
  matchPercent(match: PantryRecipeMatch): number { return Math.round(100 * match.matchedIngredientCount / match.totalIngredientCount); }
}
