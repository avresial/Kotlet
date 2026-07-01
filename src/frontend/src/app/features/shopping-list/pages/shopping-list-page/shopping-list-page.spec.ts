import { describe, expect, it } from 'vitest';
import { ShoppingListItem } from '../../shopping-list.models';
import { groupShoppingItems } from './shopping-list-page';

const item = (id: string, category: number): ShoppingListItem => ({
  id, ingredientId: id, ingredientName: id, measurementUnit: 'g', quantity: 1,
  pricePer100BaseUnits: 1, totalPrice: 1, isPurchased: false, category,
});

describe('groupShoppingItems', () => {
  it('groups categorized and unknown products in category order', () => {
    const groups = groupShoppingItems([item('apple', 21), item('other', 0), item('pear', 21)]);

    expect(groups.map(group => [group.value, group.items.map(value => value.id)])).toEqual([
      [0, ['other']], [21, ['apple', 'pear']],
    ]);
  });
});
