import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AuthService } from '../../../../core/auth/auth.service';

@Component({
  selector: 'app-home-page',
  imports: [],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  readonly auth = inject(AuthService);
  readonly firstName = computed(() => {
    const user = this.auth.currentUser();
    return user?.displayName?.trim().split(/\s+/)[0] || user?.email.split('@')[0] || 'there';
  });
  readonly today = new Intl.DateTimeFormat('en', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  }).format(new Date());

  // Mocked dashboard data — to be wired to real APIs later.
  readonly newestRecipes = [
    { emoji: '🥟', name: 'Pierogi ruskie', category: 'Main course', addedAgo: '2 days ago' },
    { emoji: '🍲', name: 'Żurek', category: 'Soup', addedAgo: '4 days ago' },
    { emoji: '🥧', name: 'Sernik', category: 'Dessert', addedAgo: '1 week ago' },
    { emoji: '🥗', name: 'Mizeria', category: 'Side', addedAgo: '1 week ago' },
  ];

  readonly shoppingList = {
    total: 12,
    done: 5,
    items: [
      { name: 'Flour', qty: '1 kg', done: true },
      { name: 'Pork loin', qty: '600 g', done: false },
      { name: 'Eggs', qty: '10', done: false },
      { name: 'Sour cream', qty: '400 ml', done: true },
      { name: 'Onions', qty: '4', done: false },
    ],
  };

  readonly todaysMenu = [
    { time: 'BREAKFAST', emoji: '🍳', name: 'Jajecznica', note: 'Scrambled eggs with chives' },
    { time: 'LUNCH', emoji: '🥪', name: 'Kanapki', note: 'Open sandwiches on rye' },
    { time: 'DINNER', emoji: '🍽️', name: 'Kotlet schabowy', note: 'A Polish classic for tonight’s table' },
  ];

  readonly topIngredients = [
    { name: 'Onion', emoji: '🧅', count: 28 },
    { name: 'Egg', emoji: '🥚', count: 24 },
    { name: 'Flour', emoji: '🌾', count: 21 },
    { name: 'Butter', emoji: '🧈', count: 18 },
    { name: 'Garlic', emoji: '🧄', count: 15 },
  ];

  readonly maxIngredientCount = Math.max(...this.topIngredients.map((i) => i.count));

  get shoppingProgress(): number {
    return Math.round((this.shoppingList.done / this.shoppingList.total) * 100);
  }
}
