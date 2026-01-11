window.mapHelpers = {
    _mapInstance: null,
    
    initMap: function (elementId, centerLat, centerLng, zoom) {
        if (typeof L === 'undefined') {
            console.error('Leaflet library not loaded');
            return null;
        }
        
        const element = document.getElementById(elementId);
        if (!element) {
            console.error(`Element with id '${elementId}' not found`);
            return null;
        }
        
        try {
            const map = L.map(elementId, {
                center: [centerLat, centerLng],
                zoom: zoom,
                zoomControl: true, // Controles de zoom por defecto
                attributionControl: true // Control de atribución
            });
            
            // Capa de OpenStreetMap
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                maxZoom: 19
            }).addTo(map);
            
            // Agregar control de escala (abajo a la izquierda)
            L.control.scale({
                position: 'bottomleft',
                metric: true,
                imperial: false
            }).addTo(map);
            
            // Agregar control de ubicación actual (si está disponible)
            if (navigator.geolocation) {
                const locateControl = L.control.locate({
                    position: 'topleft',
                    drawCircle: true,
                    follow: false,
                    setView: true,
                    keepCurrentZoomLevel: false,
                    markerOptions: {
                        title: 'Tu ubicación'
                    },
                    strings: {
                        title: 'Mostrar mi ubicación'
                    }
                }).addTo(map);
            }
            
            // Agregar control de pantalla completa
            const fullscreenControl = L.control({
                position: 'topleft'
            });
            
            fullscreenControl.onAdd = function() {
                const container = L.DomUtil.create('div', 'leaflet-bar leaflet-control');
                const button = L.DomUtil.create('a', 'leaflet-control-fullscreen', container);
                button.href = '#';
                button.title = 'Pantalla completa';
                button.innerHTML = '⛶';
                button.style.cssText = 'line-height: 30px; text-align: center; font-size: 18px; width: 30px; height: 30px; display: block;';
                
                L.DomEvent.disableClickPropagation(button);
                L.DomEvent.on(button, 'click', function(e) {
                    L.DomEvent.stopPropagation(e);
                    L.DomEvent.preventDefault(e);
                    
                    const isFullscreen = document.fullscreenElement || 
                                        document.webkitFullscreenElement || 
                                        document.mozFullScreenElement || 
                                        document.msFullscreenElement;
                    
                    if (!isFullscreen) {
                        const element = document.documentElement;
                        if (element.requestFullscreen) {
                            element.requestFullscreen();
                        } else if (element.webkitRequestFullscreen) {
                            element.webkitRequestFullscreen();
                        } else if (element.mozRequestFullScreen) {
                            element.mozRequestFullScreen();
                        } else if (element.msRequestFullscreen) {
                            element.msRequestFullscreen();
                        }
                    } else {
                        if (document.exitFullscreen) {
                            document.exitFullscreen();
                        } else if (document.webkitExitFullscreen) {
                            document.webkitExitFullscreen();
                        } else if (document.mozCancelFullScreen) {
                            document.mozCancelFullScreen();
                        } else if (document.msExitFullscreen) {
                            document.msExitFullscreen();
                        }
                    }
                });
                
                return container;
            };
            
            fullscreenControl.addTo(map);
            
            window.mapHelpers._mapInstance = map;
            console.log('Map initialized successfully');
            return map;
        } catch (error) {
            console.error('Error initializing map:', error);
            return null;
        }
    },
    
    getMap: function () {
        return window.mapHelpers._mapInstance;
    },
    
    addClusterMarker: function (lat, lng, count, dotNetRef, clusterIndex) {
        const map = window.mapHelpers._mapInstance;
        if (!map) {
            console.error('Map instance not found');
            return null;
        }
        
        if (typeof L === 'undefined') {
            console.error('Leaflet not loaded');
            return null;
        }
        
        const radius = Math.max(15, Math.min(50, 10 + count * 2));
        
        const circle = L.circleMarker([lat, lng], {
            radius: radius,
            fillColor: '#1976d2',
            color: '#fff',
            weight: 2,
            opacity: 1,
            fillOpacity: 0.7
        }).addTo(map);
        
        const label = L.divIcon({
            className: 'map-cluster-label',
            html: `<div style="
                color: white;
                font-weight: bold;
                font-size: ${radius > 30 ? '14px' : '12px'};
                text-align: center;
                line-height: ${radius * 2}px;
                width: ${radius * 2}px;
                height: ${radius * 2}px;
                display: flex;
                align-items: center;
                justify-content: center;
                pointer-events: none;
            ">${count}</div>`,
            iconSize: [radius * 2, radius * 2],
            iconAnchor: [radius, radius]
        });
        
        const marker = L.marker([lat, lng], { icon: label }).addTo(map);
        
        circle.on('click', function() {
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnClusterClick', clusterIndex).catch(err => console.error('Error calling OnClusterClick:', err));
            }
        });
        
        marker.on('click', function() {
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnClusterClick', clusterIndex).catch(err => console.error('Error calling OnClusterClick:', err));
            }
        });
        
        return { circle, marker };
    },
    
    fitBounds: function (minLat, minLng, maxLat, maxLng) {
        const map = window.mapHelpers._mapInstance;
        if (!map) return;
        map.fitBounds([[minLat, minLng], [maxLat, maxLng]], {
            padding: [50, 50]
        });
    },
    
    onMapMoveEnd: function (dotNetRef) {
        const map = window.mapHelpers._mapInstance;
        if (!map) return;
        
        map.on('moveend', function() {
            const bounds = map.getBounds();
            const zoom = map.getZoom();
            dotNetRef.invokeMethodAsync('OnMapMoveEnd', 
                bounds.getSouth(),
                bounds.getWest(),
                bounds.getNorth(),
                bounds.getEast(),
                zoom
            );
        });
    },
    
    getMapBounds: function () {
        const map = window.mapHelpers._mapInstance;
        if (!map) return null;
        const bounds = map.getBounds();
        return {
            minLat: bounds.getSouth(),
            minLng: bounds.getWest(),
            maxLat: bounds.getNorth(),
            maxLng: bounds.getEast()
        };
    },
    
    getZoom: function () {
        const map = window.mapHelpers._mapInstance;
        if (!map) return 2;
        return map.getZoom();
    },
    
    removeAllMarkers: function (markers) {
        markers.forEach(m => {
            if (m.circle) m.circle.remove();
            if (m.marker) m.marker.remove();
        });
    }
};
