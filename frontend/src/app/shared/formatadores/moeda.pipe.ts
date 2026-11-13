import { Pipe, PipeTransform } from '@angular/core';

const formatador = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

/** Exibe valores monetários em reais. Só formata: o cálculo e o arredondamento são do servidor (RN-23). */
@Pipe({ name: 'moeda' })
export class MoedaPipe implements PipeTransform {
  transform(valor: number | null | undefined): string {
    return valor === null || valor === undefined ? '' : formatador.format(valor);
  }
}
