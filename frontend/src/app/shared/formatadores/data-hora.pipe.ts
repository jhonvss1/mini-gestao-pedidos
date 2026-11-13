import { Pipe, PipeTransform } from '@angular/core';

const formatador = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'medium' });

/** Converte o instante ISO 8601 (UTC) da API para data e hora no fuso do navegador. */
@Pipe({ name: 'dataHora' })
export class DataHoraPipe implements PipeTransform {
  transform(iso: string | null | undefined): string {
    return iso ? formatador.format(new Date(iso)) : '';
  }
}
