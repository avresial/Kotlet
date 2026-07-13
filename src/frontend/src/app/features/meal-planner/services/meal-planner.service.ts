import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../../core/http/api-url';
import {
  AddMealPlanItemRequest,
  DailyMealPlan,
  HouseMember,
  MealPlanOverviewDay,
  MealPlanItem,
  MealSlot,
} from '../models/meal-planner.models';

@Injectable({ providedIn: 'root' })
export class MealPlannerService {
  private readonly http = inject(HttpClient);

  getForDate(date: string) {
    const params = new HttpParams().set('date', date);
    return this.http.get<DailyMealPlan>(apiUrl('/api/meal-planner'), { params });
  }

  getHouseMembers() {
    return this.http.get<HouseMember[]>(apiUrl('/api/meal-planner/members'));
  }

  getOverview(from: string, days: number) {
    const params = new HttpParams().set('from', from).set('days', days);
    return this.http.get<MealPlanOverviewDay[]>(apiUrl('/api/meal-planner/overview'), { params });
  }

  addItem(request: AddMealPlanItemRequest) {
    return this.http.post<MealPlanItem>(apiUrl('/api/meal-planner/items'), request);
  }

  copyDay(sourceDate: string, targetDate: string) {
    return this.http.post<DailyMealPlan>(apiUrl('/api/meal-planner/copy-day'), { sourceDate, targetDate });
  }

  copyWeek(sourceWeekStart: string, targetWeekStart: string) {
    return this.http.post<{ copied: number }>(apiUrl('/api/meal-planner/copy-week'), { sourceWeekStart, targetWeekStart });
  }

  removeItem(id: string) {
    return this.http.delete<void>(apiUrl(`/api/meal-planner/items/${id}`));
  }

  moveItem(id: string, date: string, slot: MealSlot) {
    return this.http.put<MealPlanItem>(apiUrl(`/api/meal-planner/items/${id}/move`), { date, slot });
  }

  setParticipants(id: string, userIds: string[]) {
    return this.http.put<MealPlanItem>(apiUrl(`/api/meal-planner/items/${id}/participants`), { userIds });
  }

  setParticipantPortion(id: string, userId: string, portionPercent: number) {
    return this.http.put<MealPlanItem>(
      apiUrl(`/api/meal-planner/items/${id}/participants/${userId}/portion`),
      { portionPercent },
    );
  }

  setServings(id: string, servings: number | null) {
    return this.http.put<MealPlanItem>(apiUrl(`/api/meal-planner/items/${id}/servings`), { servings });
  }

  setGuests(id: string, guests: number) {
    return this.http.put<MealPlanItem>(apiUrl(`/api/meal-planner/items/${id}/guests`), { guests });
  }
}
