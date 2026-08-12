import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../../core/http/api-url';
import {
  ContinueAsRecipeResponse,
  CreateVideoTranscriptionResponse,
  VideoTranscriptionJob,
} from '../models/tools.models';

@Injectable({ providedIn: 'root' })
export class ToolsService {
  private readonly http = inject(HttpClient);

  createVideoTranscription(url: string) {
    return this.http.post<CreateVideoTranscriptionResponse>(apiUrl('/api/tools/video-transcriptions'), { url });
  }

  getVideoTranscription(id: string) {
    return this.http.get<VideoTranscriptionJob>(apiUrl(`/api/tools/video-transcriptions/${id}`));
  }

  continueAsRecipe(id: string) {
    return this.http.post<ContinueAsRecipeResponse>(apiUrl(`/api/tools/video-transcriptions/${id}/continue-as-recipe`), {});
  }
}
