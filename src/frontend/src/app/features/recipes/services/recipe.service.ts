import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../../core/http/api-url';
import {
  CreateRecipeRequest,
  PagedResponse,
  RecipeAuditItem,
  RecipeDetail,
  RecipeImage,
  RecipeImageSourceData,
  RecipeImportDraft,
  RecipeImportJob,
  RecipeSummary,
  UpdateRecipeRequest,
} from '../models/recipe.models';

@Injectable({ providedIn: 'root' })
export class RecipeService {
  private readonly http = inject(HttpClient);

  list(page = 1, pageSize = 20, search?: string, mealType?: string, ingredientIds: readonly string[] = []) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (mealType) params = params.set('mealType', mealType);
    for (const ingredientId of ingredientIds) params = params.append('ingredientIds', ingredientId);
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

  startImport(url: string) {
    return this.http.post<{ id: string }>(apiUrl('/api/recipes/import'), { url });
  }

  getImport(id: string) {
    return this.http.get<RecipeImportJob>(apiUrl(`/api/recipes/import/${id}`));
  }

  acceptImport(id: string, draft: RecipeImportDraft) {
    return this.http.post<{ id: string }>(apiUrl(`/api/recipes/import/${id}/accept`), draft);
  }

  listAudit(limit = 5) {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<RecipeAuditItem[]>(apiUrl('/api/recipes/audit'), { params });
  }

  listRecent(limit = 4) {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<RecipeSummary[]>(apiUrl('/api/recipes/recent'), { params });
  }

  listImages(recipeId: string) {
    return this.http.get<RecipeImage[]>(apiUrl(`/api/recipes/${recipeId}/images`));
  }

  imageContent(contentUrl: `/api/${string}`) {
    return this.http.get(apiUrl(contentUrl), { responseType: 'blob' });
  }

  uploadImage(recipeId: string, file: File, altText?: string, source?: RecipeImageSourceData) {
    return this.http.post<RecipeImage>(apiUrl(`/api/recipes/${recipeId}/images`), this.imageBody(file, altText, source));
  }

  /** Same as uploadImage, but emits HttpEvents so callers can track upload progress. */
  uploadImageWithProgress(recipeId: string, file: File, altText?: string, source?: RecipeImageSourceData) {
    return this.http.post<RecipeImage>(apiUrl(`/api/recipes/${recipeId}/images`), this.imageBody(file, altText, source), {
      reportProgress: true,
      observe: 'events',
    });
  }

  private imageBody(file: File, altText?: string, source?: RecipeImageSourceData): FormData {
    const body = new FormData();
    body.append('file', file);
    if (altText?.trim()) body.append('altText', altText.trim());
    if (source) {
      body.append('sourceProvider', source.provider);
      if (source.externalId) body.append('sourceExternalId', source.externalId);
      if (source.url) body.append('sourceUrl', source.url);
      if (source.authorName) body.append('sourceAuthorName', source.authorName);
      if (source.authorUrl) body.append('sourceAuthorUrl', source.authorUrl);
    }
    return body;
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
