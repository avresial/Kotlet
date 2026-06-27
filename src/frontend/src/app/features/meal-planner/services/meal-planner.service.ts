import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../../core/http/api-url';
import { AddMealPlanItemRequest, DailyMealPlan, MealPlanItem } from '../models/meal-planner.models';

@Injectable({ providedIn: 'root' })
export class MealPlannerService {
  private readonly http = inject(HttpClient);

  getForDate(date: string) {
    const params = new HttpParams().set('date', date);
    return this.http.get<DailyMealPlan>(apiUrl('/api/meal-planner'), { params });
  }

  addItem(request: AddMealPlanItemRequest) {
    return this.http.post<MealPlanItem>(apiUrl('/api/meal-planner/items'), request);
  }

  removeItem(id: string) {
    return this.http.delete<void>(apiUrl(`/api/meal-planner/items/${id}`));
  }
}
