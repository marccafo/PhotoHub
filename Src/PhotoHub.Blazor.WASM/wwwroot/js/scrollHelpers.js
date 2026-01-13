window.scrollHelpers = {
    enableTimelineScroll: function() {
        // Agregar clase al body para aplicar estilos de scroll unificado
        document.body.classList.add('timeline-page');
        // Desactivar scroll en html y body
        document.documentElement.style.overflow = 'hidden';
        document.documentElement.style.height = '100vh';
        document.body.style.overflow = 'hidden';
        document.body.style.height = '100vh';
    },
    disableTimelineScroll: function() {
        // Remover clase del body al salir de la página
        document.body.classList.remove('timeline-page');
        // Restaurar estilos por defecto
        document.documentElement.style.overflow = '';
        document.documentElement.style.height = '';
        document.body.style.overflow = '';
        document.body.style.height = '';
    },
    getScrollContainer: function() {
        // En la página de Timeline, usar el contenedor del timeline como único scroll
        const timelineContainer = document.getElementById('timeline-scroll-container');
        if (timelineContainer) {
            return timelineContainer;
        }
        
        // Para otras páginas, mantener el comportamiento original
        const mainContent = document.querySelector('.mud-main-content');
        if (mainContent && mainContent.scrollHeight > mainContent.clientHeight) {
            const style = window.getComputedStyle(mainContent);
            if (style.overflowY !== 'hidden' && style.display !== 'none' && style.overflowY !== 'visible') {
                return mainContent;
            }
        }
        
        const layout = document.querySelector('.mud-layout');
        if (layout && layout.scrollHeight > layout.clientHeight) {
            const style = window.getComputedStyle(layout);
            if (style.overflowY !== 'hidden' && style.display !== 'none' && style.overflowY !== 'visible') {
                return layout;
            }
        }

        return window;
    },
    scrollToElement: function (id) {
        const element = document.getElementById(id);
        const scrollContainer = this.getScrollContainer();
        if (element && scrollContainer) {
            // Para el contenedor del timeline, calculamos la posición relativa
            let elementPosition;
            if (scrollContainer === window) {
                // Para window, usamos getBoundingClientRect + scrollY
                const rect = element.getBoundingClientRect();
                const appBarHeight = document.querySelector('.mud-appbar')?.offsetHeight || 0;
                elementPosition = rect.top + window.scrollY - appBarHeight - 10;
            } else {
                // Para contenedores internos (como timeline-container), usamos offsetTop
                // El offsetTop es relativo al padre posicionado
                elementPosition = element.offsetTop - 10;
            }
            
            const offsetPosition = Math.max(0, elementPosition);

            if (scrollContainer === window) {
                window.scrollTo({
                    top: offsetPosition,
                    behavior: 'smooth'
                });
            } else {
                scrollContainer.scrollTo({
                    top: offsetPosition,
                    behavior: 'smooth'
                });
            }
        }
    },
    onWindowScroll: function (dotnetHelper, groupIds) {
        const scrollContainer = this.getScrollContainer();
        
        const handleScroll = () => {
            if (window._isTimelineDragging) return;

            let activeId = "";
            
            for (const id of groupIds) {
                const element = document.getElementById(id);
                if (element) {
                    const rect = element.getBoundingClientRect();
                    // Para el contenedor del timeline, comparamos con la parte superior del contenedor
                    if (scrollContainer === window) {
                        // Para window, comparamos con la parte superior de la ventana
                        if (rect.top <= 150) { 
                            activeId = id;
                        }
                    } else {
                        // Para contenedores internos, comparamos con la parte superior del contenedor
                        const containerRect = scrollContainer.getBoundingClientRect();
                        const relativeTop = rect.top - containerRect.top;
                        if (relativeTop <= 150) { 
                            activeId = id;
                        }
                    }
                }
            }
            
            // Calcular progreso total del scroll (solo para compatibilidad con la firma del método)
            let winScroll, height;
            if (scrollContainer === window) {
                winScroll = window.scrollY || window.pageYOffset || document.documentElement.scrollTop;
                height = Math.max(
                    document.documentElement.scrollHeight - document.documentElement.clientHeight,
                    document.body.scrollHeight - document.body.clientHeight
                );
            } else {
                winScroll = scrollContainer.scrollTop;
                height = scrollContainer.scrollHeight - scrollContainer.clientHeight;
            }
            
            const scrolled = height > 0 ? (winScroll / height) * 100 : 0;
            
            // Ya no necesitamos calcular la posición del handle, pero mantenemos la firma del método
            dotnetHelper.invokeMethodAsync('OnScrollUpdated', activeId, scrolled, 0);
        };

        if (scrollContainer === window) {
            window.addEventListener('scroll', handleScroll, { passive: true });
        } else {
            scrollContainer.addEventListener('scroll', handleScroll, { passive: true });
        }
        window._timelineScrollHandler = handleScroll;
        
        // También escuchar al evento wheel y touchmove en el contenedor directamente para mayor fiabilidad
        if (scrollContainer !== window) {
            scrollContainer.addEventListener('wheel', handleScroll, { passive: true });
            scrollContainer.addEventListener('touchmove', handleScroll, { passive: true });
        }
        
        // Ejecutar inmediatamente para actualizar el estado inicial
        handleScroll();
    },
    updateScrollProgress: function (dotnetHelper) {
        const scrollContainer = this.getScrollContainer();
        if (!scrollContainer) return;
        
        let winScroll, height;
        if (scrollContainer === window) {
            winScroll = window.scrollY || window.pageYOffset || document.documentElement.scrollTop;
            height = Math.max(
                document.documentElement.scrollHeight - document.documentElement.clientHeight,
                document.body.scrollHeight - document.body.clientHeight
            );
        } else {
            winScroll = scrollContainer.scrollTop;
            height = scrollContainer.scrollHeight - scrollContainer.clientHeight;
        }
        
        const scrolled = height > 0 ? (winScroll / height) * 100 : 0;
        dotnetHelper.invokeMethodAsync('OnScrollUpdated', '', scrolled, 0);
    },
    setupTimelineHover: function (dotnetHelper, groupIds, groupLabels) {
        window._timelineGroupIds = groupIds;
        window._timelineGroupLabels = groupLabels;
        this.setupDraggableMarker(dotnetHelper);
    },
    setupDraggableMarker: function (dotnetHelper) {
        const indicator = document.querySelector('.timeline-scroll-indicator');
        const marker = document.querySelector('.timeline-scroll-marker');
        if (!indicator || !marker) return;

        let isDragging = false;

        const handleDrag = (e) => {
            if (!isDragging) return;
            
            const rect = indicator.getBoundingClientRect();
            const clientY = e.type.includes('touch') ? e.touches[0].clientY : e.clientY;
            const relativeY = clientY - rect.top;
            const percentage = Math.max(0, Math.min(100, (relativeY / rect.height) * 100));
            
            const scrollContainer = this.getScrollContainer();
            if (scrollContainer) {
                const scrollHeight = scrollContainer.scrollHeight - scrollContainer.clientHeight;
                const targetScroll = (percentage / 100) * scrollHeight;
                
                if (scrollContainer === window) {
                    window.scrollTo({ top: targetScroll });
                } else {
                    scrollContainer.scrollTop = targetScroll;
                }

                // Actualizar manualmente el estado en Blazor para feedback instantáneo
                let activeId = "";
                if (window._timelineGroupIds) {
                    for (const id of window._timelineGroupIds) {
                        const element = document.getElementById(id);
                        if (element) {
                            const rect = element.getBoundingClientRect();
                            if (scrollContainer === window) {
                                if (rect.top <= 150) activeId = id;
                            } else {
                                const containerRect = scrollContainer.getBoundingClientRect();
                                const relativeTop = rect.top - containerRect.top;
                                if (relativeTop <= 150) activeId = id;
                            }
                        }
                    }
                }
                dotnetHelper.invokeMethodAsync('OnScrollUpdated', activeId, percentage, 0);
            }
        };

        const startDragging = (e) => {
            if (e.type === 'mousedown' && e.button !== 0) return; // Solo botón izquierdo
            isDragging = true;
            window._isTimelineDragging = true;
            document.body.style.userSelect = 'none';
            document.body.style.cursor = 'grabbing';
            handleDrag(e);
        };

        const stopDragging = () => {
            if (isDragging) {
                isDragging = false;
                window._isTimelineDragging = false;
                document.body.style.userSelect = '';
                document.body.style.cursor = '';
            }
        };

        marker.addEventListener('mousedown', (e) => {
            e.stopPropagation(); // Evitar que el clic en el marker active el mousedown del indicator
            startDragging(e);
        });
        marker.addEventListener('touchstart', (e) => {
            e.stopPropagation();
            startDragging(e);
        }, { passive: false });
        
        // También permitir clic en el track para saltar
        indicator.addEventListener('mousedown', (e) => {
            // No permitir si es el marker (él tiene su propio listener con stopPropagation)
            if (e.target !== marker) {
                // Prevenir comportamiento por defecto para evitar selección de texto
                e.preventDefault();
                startDragging(e);
            }
        });

        window.addEventListener('mousemove', handleDrag);
        window.addEventListener('touchmove', handleDrag, { passive: false });
        window.addEventListener('mouseup', stopDragging);
        window.addEventListener('touchend', stopDragging);
    },
    getTimelineHoverPosition: function (dotnetHelper, clientY) {
        const indicator = document.querySelector('.timeline-scroll-indicator');
        if (!indicator || !window._timelineGroupIds || !window._timelineGroupLabels) return;
        
        const rect = indicator.getBoundingClientRect();
        const relativeY = clientY - rect.top;
        const percentage = Math.max(0, Math.min(100, (relativeY / rect.height) * 100));
        
        const scrollContainer = this.getScrollContainer();
        if (!scrollContainer) return;
        
        // Calcular la posición de scroll correspondiente al porcentaje
        const scrollHeight = scrollContainer.scrollHeight - scrollContainer.clientHeight;
        const targetScroll = (percentage / 100) * scrollHeight;
        
        // Encontrar el grupo más cercano a esa posición
        let closestIndex = 0;
        let closestDistance = Infinity;
        
        window._timelineGroupIds.forEach((groupId, index) => {
            const groupElement = document.getElementById(groupId);
            if (!groupElement) return;
            
            let groupTop;
            if (scrollContainer === window) {
                const groupRect = groupElement.getBoundingClientRect();
                groupTop = groupRect.top + window.scrollY;
            } else {
                groupTop = groupElement.offsetTop;
            }
            
            const distance = Math.abs(groupTop - targetScroll);
            if (distance < closestDistance) {
                closestDistance = distance;
                closestIndex = index;
            }
        });
        
        if (closestIndex < window._timelineGroupLabels.length) {
            const dateLabel = window._timelineGroupLabels[closestIndex];
            dotnetHelper.invokeMethodAsync('OnHoverPositionCalculated', percentage, dateLabel);
        }
    },
};

window.videoHelpers = {
    play: function (videoElement) {
        if (videoElement) {
            videoElement.play().catch(error => {
                // Ignore autoplay errors if any
                console.log("Autoplay prevented or video error: ", error);
            });
        }
    },
    pause: function (videoElement) {
        if (videoElement) {
            videoElement.pause();
            // Reset to beginning to show the first frame again
            videoElement.currentTime = 0;
        }
    }
};

window.masonryHelpers = {
    initializeMasonry: function () {
        // Procesar cada grupo de día por separado
        const dayGroups = document.querySelectorAll('.day-group');
        dayGroups.forEach(dayGroup => {
            this.justifyDayGroup(dayGroup);
        });
        
        // Configurar listener para recalcular al cambiar tamaño de ventana
        if (!window._masonryResizeHandler) {
            window._masonryResizeHandler = () => {
                const dayGroups = document.querySelectorAll('.day-group');
                dayGroups.forEach(dayGroup => {
                    window.masonryHelpers.justifyDayGroup(dayGroup);
                });
            };
            window.addEventListener('resize', window._masonryResizeHandler, { passive: true });
        }
    },
    justifyDayGroup: function (dayGroup) {
        const grid = dayGroup.querySelector('.masonry-grid');
        if (!grid) return;
        
        const items = Array.from(grid.querySelectorAll('.masonry-item'));
        if (items.length === 0) return;
        
        const gap = 2; // gap en píxeles
        const containerWidth = grid.offsetWidth;
        const itemHeight = items[0].offsetHeight || parseInt(getComputedStyle(items[0]).height) || 180;
        
        // Calcular aspect ratios de todos los items
        const aspectRatios = items.map(item => {
            let aspectRatio = parseFloat(item.getAttribute('data-aspect-ratio')) || 1.0;
            const width = parseInt(item.getAttribute('data-width')) || 0;
            const height = parseInt(item.getAttribute('data-height')) || 0;
            if (width > 0 && height > 0) {
                aspectRatio = width / height;
            }
            return aspectRatio;
        });
        
        // Distribuir items en líneas que llenen el ancho disponible
        let i = 0;
        while (i < items.length) {
            const line = [];
            const lineRatios = [];
            let lineWidth = 0;
            
            // Agregar items a la línea hasta que no quepan más
            while (i < items.length) {
                const nextItem = items[i];
                const nextRatio = aspectRatios[i];
                const nextItemWidth = itemHeight * nextRatio;
                
                // Calcular ancho de la línea si agregamos este item
                const newLineWidth = lineWidth + nextItemWidth + (line.length > 0 ? gap : 0);
                
                // Si agregar este item haría que la línea sea demasiado ancha, parar
                if (newLineWidth > containerWidth && line.length > 0) {
                    break;
                }
                
                // Agregar el item a la línea
                line.push(nextItem);
                lineRatios.push(nextRatio);
                lineWidth = newLineWidth;
                i++;
            }
            
            // Justificar la línea para llenar el ancho disponible
            if (line.length > 0) {
                this.justifyLine(line, lineRatios, containerWidth, itemHeight, gap);
            }
        }
    },
    justifyLine: function (items, aspectRatios, containerWidth, itemHeight, gap) {
        if (items.length === 0) return;
        
        // Calcular ancho disponible (descontando los gaps)
        const availableWidth = containerWidth - (items.length - 1) * gap;
        
        // Si solo hay un item, usar ancho natural pero ajustar si es necesario
        if (items.length === 1) {
            const width = Math.min(itemHeight * aspectRatios[0], availableWidth);
            items[0].style.width = width + 'px';
            return;
        }
        
        // Calcular factor de escala para llenar exactamente el ancho disponible
        // Queremos que: sum(widths) = availableWidth
        // widths[i] = itemHeight * aspectRatios[i] * scaleFactor
        // sum(itemHeight * aspectRatios[i] * scaleFactor) = availableWidth
        // scaleFactor * itemHeight * sum(aspectRatios) = availableWidth
        // scaleFactor = availableWidth / (itemHeight * sum(aspectRatios))
        const totalAspectRatio = aspectRatios.reduce((sum, ar) => sum + ar, 0);
        const scaleFactor = availableWidth / (totalAspectRatio * itemHeight);
        
        // Aplicar anchos escalados para llenar exactamente el ancho disponible
        items.forEach((item, index) => {
            const width = itemHeight * aspectRatios[index] * scaleFactor;
            item.style.width = width + 'px';
        });
    },
    updateMasonryForGroup: function (groupSelector) {
        const group = document.querySelector(groupSelector);
        if (!group) return;
        this.justifyDayGroup(group);
    }
};