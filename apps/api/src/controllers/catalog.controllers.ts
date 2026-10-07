import { Body, Controller, Delete, Get, Header, HttpCode, Inject, Param, Patch, Post, Put, Query, Req, Res } from '@nestjs/common';
import { ApiBearerAuth, ApiHeader, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { LocalPermissions, type AttractionData, type AttractionPatch, type AuthenticatedUser, type BusinessServices } from '@atracciones/business';
import {
  AttractionResponse,
  AvailabilityQuery,
  AvailabilityResponse,
  CreateAttractionRequest,
  DetailsRequest,
  ListAttractionsQuery,
  PaginatedAttractionResponse,
  SearchAttractionsRequest,
  SearchAttractionsResponse,
  UpdateAttractionRequest,
  type LocationDto,
} from '@atracciones/contracts';
import type { Response } from 'express';
import { CurrentUser, RequirePermission, RequireScope, Scopes, type AuthenticatedRequest } from '../auth/auth';
import { BUSINESS } from '../config';
import { IDEMPOTENCY_HEADER, IdempotencyKey, ParseResourceId } from '../http/request-helpers';
import { attractionLink, toAttractionResponse, toAvailabilityResponse } from '../http/responses';

const toLocation = (l: LocationDto) => ({
  address: l.address,
  city: l.city,
  country: l.country,
  latitude: l.coordinates?.latitude ?? null,
  longitude: l.coordinates?.longitude ?? null,
  type: l.type ?? null,
});

const toData = (r: CreateAttractionRequest): AttractionData => ({
  name: r.name,
  longDescription: r.longDescription,
  duration: r.duration,
  price: { currency: r.price.currency, total: r.price.total },
  categories: r.categories,
  badges: r.badges ?? [],
  locations: r.locations.map(toLocation),
  photoUrls: (r.photos ?? []).map((p) => p.url),
  operator: r.operator ? { id: r.operator.id, name: r.operator.name } : null,
  productType: r.productType,
  includes: r.includes ?? [],
  supportedLanguages: r.supportedLanguages ?? [],
  freeCancellation: r.freeCancellation ?? false,
});

const toPatch = (r: UpdateAttractionRequest): AttractionPatch => ({
  name: r.name,
  longDescription: r.longDescription,
  duration: r.duration,
  price: r.price ? { currency: r.price.currency, total: r.price.total } : null,
  categories: r.categories,
  badges: r.badges,
  locations: r.locations?.map(toLocation) ?? null,
  photoUrls: r.photos?.map((p) => p.url) ?? null,
  operator: r.operator ? { id: r.operator.id, name: r.operator.name } : null,
  productType: r.productType,
  includes: r.includes,
  supportedLanguages: r.supportedLanguages,
  freeCancellation: r.freeCancellation,
});

const idempotencyDoc = ApiHeader({ name: IDEMPOTENCY_HEADER, required: true, description: 'UUID único por intento lógico de la operación.' });

@ApiTags('Catálogo')
@ApiBearerAuth()
@Controller('atracciones')
export class AttractionsController {
  constructor(@Inject(BUSINESS) private readonly business: BusinessServices) {}

  @Post('search')
  @HttpCode(200)
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Buscar atracciones por destino, fechas y valoración' })
  @ApiResponse({ status: 200, type: SearchAttractionsResponse })
  async search(@Body() body: SearchAttractionsRequest, @Req() request: AuthenticatedRequest): Promise<SearchAttractionsResponse> {
    const result = await this.business.attractions.search({
      currency: body.currency ?? null,
      cities: body.cities ?? [],
      countries: body.countries ?? [],
      startDate: body.dates?.startDate ?? null,
      endDate: body.dates?.endDate ?? null,
      minimumReviewScore: body.filters?.rating?.minimumReviewScore ?? null,
      minimumReviewCount: body.filters?.rating?.minimumReviewCount ?? null,
      nextPage: body.nextPage ?? null,
      rows: body.rows ?? 20,
      sortBy: body.sort?.by ?? 'most_popular',
    });
    return {
      data: result.items.map(toAttractionResponse),
      metadata: { totalResults: result.totalResults, nextPage: result.nextPage },
      requestId: request.id ?? '',
    };
  }

  @Post('details')
  @HttpCode(200)
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Obtener varias atracciones por identificador' })
  @ApiResponse({ status: 200, type: SearchAttractionsResponse })
  async details(@Body() body: DetailsRequest, @Req() request: AuthenticatedRequest): Promise<SearchAttractionsResponse> {
    const result = await this.business.attractions.getDetails(body.attractions, body.languages ?? []);
    return { data: result.map(toAttractionResponse), metadata: { totalResults: result.length, nextPage: null }, requestId: request.id ?? '' };
  }

  @Get()
  @RequireScope(Scopes.Read)
  @Header('Cache-Control', 'private, max-age=60')
  @ApiOperation({ summary: 'Listar atracciones paginadas' })
  @ApiResponse({ status: 200, type: PaginatedAttractionResponse })
  async list(@Query() query: ListAttractionsQuery): Promise<PaginatedAttractionResponse> {
    const page = await this.business.attractions.list(query.limit, query.offset);
    return {
      data: page.items.map(toAttractionResponse),
      meta: { totalItems: page.totalItems, itemCount: page.items.length, itemsPerPage: page.limit, totalPages: page.totalPages, currentPage: page.currentPage },
    };
  }

  @Post()
  @RequireScope(Scopes.Write)
  @RequirePermission(LocalPermissions.CatalogWrite)
  @idempotencyDoc
  @ApiOperation({ summary: 'Crear una atracción (administración)' })
  @ApiResponse({ status: 201, type: AttractionResponse })
  async create(
    @Body() body: CreateAttractionRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() actor: AuthenticatedUser,
    @Res({ passthrough: true }) response: Response,
  ): Promise<AttractionResponse> {
    const result = await this.business.attractions.create({ data: toData(body), idempotencyKey: key, actor });
    response.status(201).location(attractionLink(result.id));
    return toAttractionResponse(result);
  }

  @Get(':id')
  @RequireScope(Scopes.Read)
  @Header('Cache-Control', 'private, max-age=60')
  @ApiOperation({ summary: 'Detalle de una atracción' })
  @ApiResponse({ status: 200, type: AttractionResponse })
  async get(@Param('id', ParseResourceId) id: string): Promise<AttractionResponse> {
    return toAttractionResponse(await this.business.attractions.get(id));
  }

  @Put(':id')
  @HttpCode(204)
  @RequireScope(Scopes.Write)
  @RequirePermission(LocalPermissions.CatalogWrite)
  @idempotencyDoc
  @ApiOperation({ summary: 'Reemplazar una atracción (administración)' })
  async replace(
    @Param('id', ParseResourceId) id: string,
    @Body() body: CreateAttractionRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() actor: AuthenticatedUser,
  ): Promise<void> {
    await this.business.attractions.replace({ attractionId: id, data: toData(body), idempotencyKey: key, actor });
  }

  @Patch(':id')
  @RequireScope(Scopes.Write)
  @RequirePermission(LocalPermissions.CatalogWrite)
  @idempotencyDoc
  @ApiOperation({ summary: 'Modificar parcialmente una atracción (administración)' })
  @ApiResponse({ status: 200, type: AttractionResponse })
  async patch(
    @Param('id', ParseResourceId) id: string,
    @Body() body: UpdateAttractionRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() actor: AuthenticatedUser,
  ): Promise<AttractionResponse> {
    return toAttractionResponse(await this.business.attractions.patch({ attractionId: id, patch: toPatch(body), idempotencyKey: key, actor }));
  }

  @Delete(':id')
  @HttpCode(204)
  @RequireScope(Scopes.Write)
  @RequirePermission(LocalPermissions.CatalogWrite)
  @idempotencyDoc
  @ApiOperation({ summary: 'Eliminar una atracción sin reservas activas (administración)' })
  async delete(@Param('id', ParseResourceId) id: string, @IdempotencyKey() key: string, @CurrentUser() actor: AuthenticatedUser): Promise<void> {
    await this.business.attractions.delete({ attractionId: id, idempotencyKey: key, actor });
  }

  @Get(':id/availability')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Disponibilidad de una fecha (hora local de la atracción)' })
  @ApiResponse({ status: 200, type: AvailabilityResponse })
  async availability(@Param('id', ParseResourceId) id: string, @Query() query: AvailabilityQuery): Promise<AvailabilityResponse> {
    return toAvailabilityResponse(await this.business.availability.get(id, query.date));
  }
}
