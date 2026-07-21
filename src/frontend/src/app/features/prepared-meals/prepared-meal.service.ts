import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';
import { PreparedMeal, PreparedMealImage, PreparedMealRequest } from './prepared-meal.models';

@Injectable({ providedIn: 'root' })
export class PreparedMealService {
  private readonly http = inject(HttpClient);
  list(includeArchived = false) { return this.http.get<PreparedMeal[]>(apiUrl('/api/prepared-meals'), { params: new HttpParams().set('includeArchived', includeArchived) }); }
  create(request: PreparedMealRequest) { return this.http.post<PreparedMeal>(apiUrl('/api/prepared-meals'), request); }
  update(id: string, request: PreparedMealRequest) { return this.http.put<PreparedMeal>(apiUrl(`/api/prepared-meals/${id}`), request); }
  archive(id: string) { return this.http.delete<void>(apiUrl(`/api/prepared-meals/${id}`)); }
  restore(id: string) { return this.http.post<void>(apiUrl(`/api/prepared-meals/${id}/restore`), {}); }
  listImages(id: string) { return this.http.get<PreparedMealImage[]>(apiUrl(`/api/prepared-meals/${id}/images`)); }
  getImageContent(image: PreparedMealImage) { return this.http.get(apiUrl(image.contentUrl), { responseType: 'blob' }); }
  uploadImage(id: string, file: File, altText: string) { const body = new FormData(); body.append('file', file); body.append('altText', altText); return this.http.post<PreparedMealImage>(apiUrl(`/api/prepared-meals/${id}/images`), body); }
  updateImage(id: string, imageId: string, altText: string) { return this.http.patch<void>(apiUrl(`/api/prepared-meals/${id}/images/${imageId}`), { altText }); }
  reorderImages(id: string, imageIds: string[]) { return this.http.put<void>(apiUrl(`/api/prepared-meals/${id}/images/order`), { imageIds }); }
  deleteImage(id: string, imageId: string) { return this.http.delete<void>(apiUrl(`/api/prepared-meals/${id}/images/${imageId}`)); }
}
