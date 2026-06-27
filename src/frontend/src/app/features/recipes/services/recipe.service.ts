import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../../core/http/api-url';
import {
  CreateRecipeRequest,
  PagedResponse,
  RecipeDetail,
  RecipeImage,
  RecipeSummary,
  UpdateRecipeRequest,
} from '../models/recipe.models';

@Injectable({ providedIn: 'root' })
export class RecipeService {
  private readonly http = inject(HttpClient);

  list(page = 1, pageSize = 20, search?: string) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedResponse<RecipeSummary>>(apiUrl('/api/recipes'), { params });
  }

  get(id: string) {
    return this.http.get<RecipeDetail>(apiUrl(`/api/recipes/${id}`));
  }

  create(request: CreateRecipeRequest) {
    return this.http.post<RecipeDetail>(apiUrl('/api/recipes'), request);
  }

  update(id: string, request: UpdateRecipeRequest) {
    return this.http.put<RecipeDetail>(apiUrl(`/api/recipes/${id}`), request);
  }

  delete(id: string) {
    return this.http.delete<void>(apiUrl(`/api/recipes/${id}`));
  }

  listImages(recipeId: string) {
    return this.http.get<RecipeImage[]>(apiUrl(`/api/recipes/${recipeId}/images`));
  }

  imageContent(contentUrl: `/api/${string}`) {
    return this.http.get(apiUrl(contentUrl), { responseType: 'blob' });
  }

  uploadImage(recipeId: string, file: File, altText?: string) {
    const body = new FormData();
    body.append('file', file);
    if (altText?.trim()) body.append('altText', altText.trim());
    return this.http.post<RecipeImage>(apiUrl(`/api/recipes/${recipeId}/images`), body);
  }

  updateImage(recipeId: string, imageId: string, altText: string | null) {
    return this.http.patch<RecipeImage>(apiUrl(`/api/recipes/${recipeId}/images/${imageId}`), { altText });
  }

  reorderImages(recipeId: string, imageIds: string[]) {
    return this.http.put<void>(apiUrl(`/api/recipes/${recipeId}/images/order`), { imageIds });
  }

  deleteImage(recipeId: string, imageId: string) {
    return this.http.delete<void>(apiUrl(`/api/recipes/${recipeId}/images/${imageId}`));
  }
}
