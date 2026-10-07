import { beginLogin, completeLogin, logout } from '@/features/auth/oauth';
import type { RegisterProfileRequest, RegisterProfileResponse, User } from '@/types/identity';
import { isApiError } from '@/utils/api';
import { http } from './client';

/** Registro del perfil local: la identidad sale de los claims del token, no del cuerpo. */
export const registerProfile = (body: RegisterProfileRequest = {}) =>
  http.post<RegisterProfileResponse>('/auth/register', body);

export const getMe = (signal?: AbortSignal) => http.get<User>('/users/me', { signal });

/** Devuelve el usuario local; si el perfil aún no existe lo registra (primer inicio de sesión). */
export async function ensureProfile(): Promise<User> {
  try {
    return await getMe();
  } catch (error) {
    if (!isApiError(error, 'notFound') && !(isApiError(error) && error.code === 'PROFILE_NOT_REGISTERED')) throw error;
    await registerProfile();
    return getMe();
  }
}

export { beginLogin as login, completeLogin as handleCallback, logout };
