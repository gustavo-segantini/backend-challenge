import { useEffect, useRef } from 'react';
import api from '../services/api';

/**
 * Hook para fazer polling do status dos uploads em tempo real
 * @param {Function} onUpdate - Callback chamado quando os uploads são atualizados
 * @param {Object} options - Opções de configuração
 * @param {number} options.interval - Intervalo de polling em ms (padrão: 2000ms)
 * @param {boolean} options.enabled - Se o polling está habilitado (padrão: true)
 * @param {Array} options.statusFilter - Filtrar por status específicos (ex: ['Processing', 'Pending'])
 */
export function useUploadStatusPolling(onUpdate, options = {}) {
  const {
    interval = 2000, // 2 segundos por padrão
    enabled = true,
    statusFilter = null,
    page = 1,
    pageSize = 20
  } = options;

  const intervalRef = useRef(null);
  const isPollingRef = useRef(false);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    let retryTimeout = null;

    const pollUploads = async () => {
      // Evitar múltiplas chamadas simultâneas
      if (isPollingRef.current) {
        return;
      }

      try {
        isPollingRef.current = true;
        const params = {
          page,
          pageSize,
          ...(statusFilter && { status: statusFilter })
        };
        
        const response = await api.get('/transactions/uploads', { params });
        const uploads = response.data.items || [];
        
        // Chamar callback com os dados atualizados
        onUpdate(uploads, response.data);
      } catch (error) {
        console.error('Error polling upload status:', error);
        // Em caso de erro, continuar tentando mas com intervalo maior
        // Não parar o polling, apenas logar o erro
      } finally {
        isPollingRef.current = false;
      }
    };

    const startPolling = () => {
      // Limpar qualquer timeout de retry anterior
      if (retryTimeout) {
        clearTimeout(retryTimeout);
        retryTimeout = null;
      }

      // Fazer primeira chamada imediatamente
      pollUploads();
      
      // Configurar polling periódico
      intervalRef.current = setInterval(pollUploads, interval);
    };

    startPolling();

    // Cleanup
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
      if (retryTimeout) {
        clearTimeout(retryTimeout);
        retryTimeout = null;
      }
    };
  }, [enabled, interval, onUpdate, statusFilter, page, pageSize]);

  // Função para parar o polling manualmente
  const stopPolling = () => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  };

  // Função para reiniciar o polling
  const restartPolling = () => {
    stopPolling();
    if (enabled) {
      const pollUploads = async () => {
        if (isPollingRef.current) return;
        try {
          isPollingRef.current = true;
          const params = {
            page,
            pageSize,
            ...(statusFilter && { status: statusFilter })
          };
          const response = await api.get('/transactions/uploads', { params });
          onUpdate(response.data.items || [], response.data);
        } catch (error) {
          console.error('Error polling upload status:', error);
        } finally {
          isPollingRef.current = false;
        }
      };
      pollUploads();
      intervalRef.current = setInterval(pollUploads, interval);
    }
  };

  return { stopPolling, restartPolling };
}

